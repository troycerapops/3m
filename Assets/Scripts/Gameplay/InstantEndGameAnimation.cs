using System;
using ThreeMusketeers.Core;

namespace ThreeMusketeers.Gameplay
{
    /// <summary>Default IEndGameAnimation: no animation, completes immediately.</summary>
    public class InstantEndGameAnimation : IEndGameAnimation
    {
        public void Play(BoardState board, GameResult result, Board3DView boardView, Action onComplete) =>
            onComplete?.Invoke();
    }
}