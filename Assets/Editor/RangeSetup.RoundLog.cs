using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IntuitiveDesigns.ShootingRange.EditorTools
{
    /// Step 73: the round log for balancing (RoundLog), on the game object, wired to the director, the power-ups and
    /// both guns. Prints in development builds only. Fills blanks: a game that has one is left alone
    public static partial class RangeSetup
    {
        [MenuItem("Shooting Range/Setup/73 - Add The Round Log", false, 96)]
        public static void RoundLogMenu()
        {
            Begin("Round log");
            var game = Object.FindObjectOfType<RangeGame>();
            var director = Object.FindObjectOfType<TrackDirector>();
            var powerUps = Object.FindObjectOfType<PowerUps>();
            var pistols = new List<Object>(Object.FindObjectsOfType<Pistol>());

            if (game == null || director == null || powerUps == null) Fail("The scene needs a RangeGame, a TrackDirector and PowerUps");
            else if (game.GetComponent<RoundLog>() != null) Say(game.name + " already has a round log");
            else
            {
                var log = Undo.AddComponent<RoundLog>(game.gameObject);
                new Fields(log).Set("game", game).Set("director", director).Set("powerUps", powerUps)
                               .SetArray("pistols", pistols).Apply();
                Did(game.name + ": round log, " + pistols.Count + " gun(s). One line a round in development builds");
                Dirty();
            }
            End();
        }
    }
}
