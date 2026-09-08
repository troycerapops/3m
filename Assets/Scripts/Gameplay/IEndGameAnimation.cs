using System;
using ThreeMusketeers.Core;

namespace ThreeMusketeers.Gameplay
{
    /// <summary>
    /// Extension seam for what happens visually when the game ends. Left
    /// deliberately unimplemented for now -- offense and defense wins will
    /// likely want different celebration animations, and pieces may animate
    /// through BorderLayout cells and/or Board3DView.ExitPoints. GameManager
    /// calls Play() once and only shows the win overlay once onComplete
    /// fires, so a real implementation can take as long as it needs. See
    /// InstantEndGameAnimation for today's no-op default.
    /// </summary>
    public interface IEndGameAnimation
    {
        void Play(BoardState board, GameResult result, Board3DView boardView, Action onComplete);
    }
}