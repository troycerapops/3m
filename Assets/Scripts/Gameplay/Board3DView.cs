using System;
using System.Collections.Generic;
using ThreeMusketeers.Core;
using ThreeMusketeers.Theming;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ThreeMusketeers.Gameplay
{
    /// <summary>
    /// Builds and drives the 5x5 board as real 3D GameObjects (world space,
    /// fixed camera -- see BoardCameraRig), themed entirely through a
    /// ThemeDefinition. Nothing here is prison-specific; it only ever asks
    /// the theme for prefabs/colors/text.
    ///
    /// Layout convention (this is also what the Blender art spec document
    /// hands your son): 1 world unit per cell, board centered on the origin,
    /// Coord.X -> world X, Coord.Y -> world Z, ground plane at Y = 0.
    /// </summary>
    public class Board3DView : MonoBehaviour
    {
        public event Action<Coord> CellClicked;

        [Header("Theme (swap this to reskin the whole board)")]
        [SerializeField] private ThemeDefinition theme;

        /// <summary>
        /// Single source of truth for "which theme is active" -- GameManager
        /// reads this to compose UI text rather than holding its own
        /// (potentially mismatched) reference to a theme asset.
        /// </summary>
        public ThemeDefinition Theme => theme;

        [Header("Input")]
        [Tooltip("Defaults to Camera.main if left empty.")]
        [SerializeField] private Camera boardCamera;
        [Tooltip("Restrict tap raycasts to these layers. Defaults to Everything, which is fine until an " +
                 "environment prefab's own colliders start overlapping the board -- at that point put tiles " +
                 "on their own layer and narrow this, rather than the environment silently eating taps.")]
        [SerializeField] private LayerMask tileRaycastMask = ~0;

        [Header("Layout -- must match the Blender art spec")]
        [SerializeField] private float cellSize = 1f;

        [Header("Lighting")]
        [Tooltip("The scene's default light(s) (e.g. the Directional Light). Disabled automatically " +
                 "whenever the active theme sets suppliesOwnLighting = true; re-enabled otherwise. " +
                 "Leave empty if you don't need this yet -- nothing else depends on it.")]
        [SerializeField] private Light[] defaultSceneLights;

        private Transform _boardRoot;
        private Transform _tilesRoot;
        private Transform _piecesRoot;
        private readonly Dictionary<Coord, Tile3D> _tiles = new Dictionary<Coord, Tile3D>();
        private readonly Dictionary<Coord, PieceView3D> _pieces = new Dictionary<Coord, PieceView3D>();
        private readonly List<Transform> _exitPoints = new List<Transform>();
        private bool _inputEnabled = true;

        /// <summary>
        /// Transforms found under the environment prefab whose name starts with
        /// "ExitPoint" (e.g. "ExitPoint_North") -- see Blender_Art_Spec.md. Empty
        /// until a theme with an environment prefab containing such markers is
        /// active. Consumed by future IEndGameAnimation implementations.
        /// </summary>
        public IReadOnlyList<Transform> ExitPoints => _exitPoints;

        private void Awake()
        {
            if (boardCamera == null) boardCamera = Camera.main;

            // No theme assigned in the Inspector? Fall back to an in-memory
            // ThemeDefinition with its untouched default field values (never
            // saved to disk) rather than leaving `theme` null. This is what
            // guarantees a fully playable default board -- a checkerboard
            // floor with clearly distinguishable placeholder pieces and
            // working turn/win text -- with zero setup, and it's exactly
            // what you'd get from creating a real ThemeDefinition asset and
            // not touching any of its fields.
            if (theme == null) theme = ScriptableObject.CreateInstance<ThemeDefinition>();
        }

        /// <summary>Full (re)build: tears down any previous board and spawns tiles + pieces matching the given state. Used on start and on restart.</summary>
        public void BuildBoard(BoardState board)
        {
            if (_boardRoot != null) Destroy(_boardRoot.gameObject);
            _tiles.Clear();
            _pieces.Clear();
            _exitPoints.Clear();

            _boardRoot = new GameObject("BoardRoot").transform;
            _boardRoot.SetParent(transform, false);
            _tilesRoot = new GameObject("Tiles").transform;
            _tilesRoot.SetParent(_boardRoot, false);
            _piecesRoot = new GameObject("Pieces").transform;
            _piecesRoot.SetParent(_boardRoot, false);

            for (int x = 0; x < BoardState.BoardSize; x++)
            for (int y = 0; y < BoardState.BoardSize; y++)
            {
                var coord = new Coord(x, y);
                bool isLight = (x + y) % 2 != 0;

                var tileGo = ThemeVisualFactory.CreateTile(theme, isLight, _tilesRoot);
                tileGo.transform.localPosition += CoordToLocalPosition(coord);

                var tile3d = tileGo.GetComponent<Tile3D>();
                if (tile3d == null) tile3d = tileGo.AddComponent<Tile3D>();
                tile3d.Initialize(coord, tileGo.GetComponentInChildren<Renderer>());
                _tiles[coord] = tile3d;

                var pieceType = board.GetPiece(coord);
                if (pieceType != PieceType.None)
                    SpawnPiece(pieceType, coord);
            }

            if (theme != null && theme.environmentPrefab != null)
            {
                var env = Instantiate(theme.environmentPrefab, _boardRoot);
bool themeSuppliesLighting = theme != null && theme.suppliesOwnLighting;
foreach (var light in defaultSceneLights)
    if (light != null) light.enabled = !themeSuppliesLighting;
                // Collect artist-placed exit/anchor markers (any child named
                // "ExitPoint*") for future end-of-game animations. See
                // Blender_Art_Spec.md for the naming convention.
                foreach (var child in env.GetComponentsInChildren<Transform>(true))
                    if (child.name.StartsWith("ExitPoint", StringComparison.OrdinalIgnoreCase))
                        _exitPoints.Add(child);
            }
        }

        private void SpawnPiece(PieceType type, Coord coord)
        {
            var pieceGo = ThemeVisualFactory.CreatePiece(theme, type, _piecesRoot);

            // Factory already set a Y offset appropriate for the piece's pivot
            // convention (see ThemeVisualFactory / the art spec); we only need
            // to place it in the right cell on the XZ plane.
            var local = pieceGo.transform.localPosition;
            var cellLocal = CoordToLocalPosition(coord);
            local.x = cellLocal.x;
            local.z = cellLocal.z;
            pieceGo.transform.localPosition = local;

            var view = pieceGo.GetComponent<PieceView3D>();
            if (view == null) view = pieceGo.AddComponent<PieceView3D>();
            view.Initialize(type, coord);
            _pieces[coord] = view;
        }

        /// <summary>
        /// Pure formula, no bounds-checking -- works for any integer coordinate,
        /// including cells outside the playable 5x5 grid (see BorderLayout), so
        /// border/exit-related code needs no special-casing here.
        /// </summary>
        public Vector3 CoordToLocalPosition(Coord coord)
        {
            float half = (BoardState.BoardSize - 1) / 2f;
            return new Vector3((coord.X - half) * cellSize, 0f, (coord.Y - half) * cellSize);
        }

        /// <summary>
        /// Reflects a move that was just applied to BoardState: animates the
        /// moving piece and destroys a captured one, without rebuilding
        /// anything else. Call this AFTER BoardState.ApplyMove.
        /// </summary>
        public void ApplyMoveVisual(Move move)
        {
            if (_pieces.TryGetValue(move.To, out var captured))
            {
                captured.AnimateCapturedThenDestroy();
                _pieces.Remove(move.To);
            }

            if (_pieces.TryGetValue(move.From, out var mover))
            {
                _pieces.Remove(move.From);
                var cellLocal = CoordToLocalPosition(move.To);
                var targetPos = new Vector3(cellLocal.x, mover.transform.localPosition.y, cellLocal.z);
                mover.AnimateTo(move.To, targetPos);
                _pieces[move.To] = mover;
            }
            else
            {
                Debug.LogWarning($"Board3DView.ApplyMoveVisual: no piece view found at {move.From} for move {move}.");
            }
        }

        /// <summary>
        /// movablePieces: coordinates of pieces GameManager wants glowing as a
        /// "you can move this" hint (already filtered to the current player's
        /// pieces that actually have a legal move right now, and to only
        /// appear once GameManager's hint delay has elapsed). Pass null to
        /// show no hint glow at all.
        /// </summary>
        public void SetSelection(Coord? selected, IReadOnlyList<Coord> legalDestinations, IReadOnlyList<Coord> movablePieces)
        {
            HashSet<Coord> destSet = legalDestinations != null ? new HashSet<Coord>(legalDestinations) : null;
            HashSet<Coord> movableSet = movablePieces != null ? new HashSet<Coord>(movablePieces) : null;
            foreach (var kvp in _tiles)
            {
                bool isSelected = selected.HasValue && selected.Value == kvp.Key;
                bool isDest = destSet != null && destSet.Contains(kvp.Key);
                bool hasLegalMove = movableSet != null && movableSet.Contains(kvp.Key);
                kvp.Value.SetHighlight(isSelected, isDest, hasLegalMove);
            }
        }

        public void SetBoardInteractable(bool interactable) => _inputEnabled = interactable;

        private void Update()
        {
            if (!_inputEnabled || boardCamera == null) return;

            // Pointer.current unifies mouse (Editor/desktop) and touch
            // (Android) behind one API -- no separate code paths needed.
            // Requires the Input System package with Active Input Handling
            // set to "Input System Package (New)" (see README).
            var pointer = Pointer.current;
            if (pointer == null) return;
            if (!pointer.press.wasPressedThisFrame) return;

            // Don't let a tap on a UI element (e.g. the restart button) also
            // register as a board tap underneath it.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 screenPos = pointer.position.ReadValue();
            var ray = boardCamera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, tileRaycastMask))
            {
                var piece = hitInfo.collider.GetComponentInParent<PieceView3D>();
                if (piece != null) { CellClicked?.Invoke(piece.Coord); return; }

                var tile = hitInfo.collider.GetComponentInParent<Tile3D>();
                if (tile != null) CellClicked?.Invoke(tile.Coord);
            }
        }
    }
}