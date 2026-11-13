using System;
using Ibit.Core.Audio;
using UnityEngine;

namespace Ibit.Core.Game
{
    public partial class GameManager
    {
        public static Action OnGamePause;
        public static Action OnGameUnPause;

        private static bool legacyPauseRequested;
        private static bool fingerPlacementPauseRequested;
        public static bool GameIsPaused { get; private set; }
        // Lets high-priority legacy security/manual panels take visual priority.
        public static bool HasLegacyPauseRequest => legacyPauseRequested;

        // Legacy callers (manual menus and clinical security panels) retain
        // their own hold, even if the finger-placement overlay is already up.
        public static void PauseGame()
        {
            legacyPauseRequested = true;
            ApplyPauseRequests();
        }

        public static void UnPauseGame()
        {
            legacyPauseRequested = false;
            ApplyPauseRequests();
        }

        // This acquisition must NOT steal an existing manual/security pause.
        public static bool TryPauseForFingerPlacement()
        {
            if (GameIsPaused || fingerPlacementPauseRequested)
                return false;
            fingerPlacementPauseRequested = true;
            ApplyPauseRequests();
            return true;
        }

        public static void ReleaseFingerPlacementPause()
        {
            if (!fingerPlacementPauseRequested)
                return;
            fingerPlacementPauseRequested = false;
            ApplyPauseRequests();
        }

        private static void ApplyPauseRequests()
        {
            bool shouldPause = legacyPauseRequested || fingerPlacementPauseRequested;
            if (shouldPause == GameIsPaused) return;

            if (shouldPause)
                SoundManager.Instance?.PlaySound("GamePause");
            else
                SoundManager.Instance?.PlaySound("GameUnPause");

            Time.timeScale = shouldPause ? 0f : 1f;
            GameIsPaused = shouldPause;
            if (shouldPause)
                OnGamePause?.Invoke();
            else
                OnGameUnPause?.Invoke();
        }
    }
}
