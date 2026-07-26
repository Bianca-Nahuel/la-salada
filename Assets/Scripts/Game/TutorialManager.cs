using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Salada.Placement;
using Salada.Combat;
using Salada.UI;

namespace Salada.Game
{
    /// <summary>
    /// Tutorial scripteado que corre al iniciar la partida (reemplaza el evento inicial del encargado).
    /// Va guiando paso a paso con dialogos modales, limitando lo que se puede hacer. Al terminar deja
    /// las 4 balanzas en 50 y el juego sigue normal. Etapa 1: intro, celular, primer puesto, primer
    /// cliente y las 4 estadisticas. (La etapa 2 -rival + disputa- se agrega despues.)
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        [SerializeField] private bool runTutorial = true;
        [SerializeField] private TutorialScript script;
        [SerializeField] private float statBackdropAlpha = 0.42f; // fondo mas transparente al resaltar una stat

        private PhoneUI _phone;
        private PlacementController _placement;
        private WaveManager _waves;
        private BusinessMeters _meters;
        private EventManager _events;
        private TutorialDialog _dialog;
        private GridManager _grid;
        private TerritoryManager _territory;

        void Start()
        {
            _phone = FindAnyObjectByType<PhoneUI>();
            _placement = FindAnyObjectByType<PlacementController>();
            _waves = FindAnyObjectByType<WaveManager>();
            _meters = FindAnyObjectByType<BusinessMeters>();
            _events = FindAnyObjectByType<EventManager>();
            _dialog = FindAnyObjectByType<TutorialDialog>(FindObjectsInactive.Include);
            _grid = FindAnyObjectByType<GridManager>();
            _territory = FindAnyObjectByType<TerritoryManager>();

            if (!runTutorial || script == null || _dialog == null) return;
            if (_events != null) _events.SuppressInitialDay = true; // el tutorial reemplaza el evento inicial
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            // celular guardado hasta que el encargado te lo entrega
            if (_phone != null) _phone.gameObject.SetActive(false);

            yield return Say(script.intro, script.continueLabel);

            if (_phone != null) _phone.gameObject.SetActive(true);
            yield return null; // dejar que reconstruya
            yield return Say(script.givePhone, script.continueLabel);

            // ---- primer puesto: solo se puede construir el simple, en una sola celda ----
            var buildCell = FirstBuildCell();
            if (_placement != null) _placement.TutorialCells = new HashSet<Vector2Int> { buildCell };
            if (_phone != null) _phone.TutorialSpotlightButton("Build_0");
            yield return Say(script.placeFirstStall, "Entendido", statBackdropAlpha);
            yield return new WaitUntil(() => IsPlayerStall(buildCell));
            if (_placement != null) _placement.TutorialCells = null;

            // ---- primer cliente: anuncio ANTES, aparece, llega, compra, camina, y "venta realizada" DESPUES ----
            yield return Say(script.firstClientComing, script.continueLabel);
            if (_phone != null) _phone.TutorialSpotlightButton(""); // todo bloqueado mientras mira
            yield return SendClient(buildCell);
            if (_phone != null) _phone.ClearTutorial();
            yield return Say(script.saleDone, script.continueLabel);

            // ---- las 4 estadisticas (una por dialogo, resaltando el medidor) ----
            yield return StatStep(script.statProfit, "Meter_Ganancias");
            yield return StatStep(script.statHostility, "Meter_Hostilidad");
            yield return StatStep(script.statReputation, "Meter_Reputacion");
            yield return StatStep(script.statHappiness, "Meter_ClimaLaboral");
            yield return Say(script.statsWarning, script.continueLabel);

            // ---- etapa 2: rival + disputa ----
            char zone = _grid.Model.ZoneOf(buildCell);
            var used = new HashSet<Vector2Int> { buildCell };
            var redCell = FindCellInZone(zone, buildCell, used);
            var rivalData = _waves != null ? _waves.expansionStallData : null;
            if (redCell.HasValue && rivalData != null)
            {
                used.Add(redCell.Value);
                var facing = _grid.Model.FacingToAisle(redCell.Value);
                _grid.SpawnStall(redCell.Value, Vector2Int.one, Owner.Enemy, facing, rivalData);
            }
            yield return Say(script.rivalAppears, script.continueLabel);

            // cliente disputado que se lleva el rojo (esperamos que llegue, cambie de ropa y camine, se ve el robo)
            if (redCell.HasValue)
            {
                if (_phone != null) _phone.TutorialSpotlightButton("");
                yield return SendClient(redCell.Value, TopRightOpening()); // entra por arriba-derecha -> lo gana el rojo
                if (_phone != null) _phone.ClearTutorial();
            }
            yield return Say(script.rivalStoleClient, script.continueLabel);

            // obligar a poner un puesto en la MISMA zona que el rojo
            var competeCell = FindCellInZone(zone, redCell ?? buildCell, used);
            if (competeCell.HasValue)
            {
                if (_placement != null) _placement.TutorialCells = new HashSet<Vector2Int> { competeCell.Value };
                if (_phone != null) _phone.TutorialSpotlightButton("Build_0");
                yield return Say(script.mustCompete, "Entendido", statBackdropAlpha);
                yield return new WaitUntil(() => IsPlayerStall(competeCell.Value));
                if (_placement != null) _placement.TutorialCells = null;
            }

            // abrir menu de zonas -> atacar -> minijuego (mas facil) -> penalizacion
            var minigame = FindAnyObjectByType<Salada.UI.DisputeMinigame>(FindObjectsInactive.Include);
            if (minigame != null) minigame.SetEasyMode(true);
            if (_phone != null) _phone.TutorialSpotlightButton("Zonas");
            yield return Say(script.explainDispute, "Entendido", statBackdropAlpha);
            while (minigame != null && !minigame.IsShowing) yield return null;   // esperar a que lo dispare
            while (minigame != null && minigame.IsShowing) yield return null;    // esperar a que lo complete
            if (minigame != null) minigame.SetEasyMode(false);
            if (_phone != null) _phone.ClearTutorial();

            // ---- cierre ----
            yield return Say(script.finish, "¡A jugar!");
            if (_phone != null) _phone.ClearTutorial();
            if (_meters != null) _meters.SetAll(50f); // todo queda en 50
        }

        // ---- pasos ----

        IEnumerator Say(string text, string label, float alpha = 0.65f)
        {
            bool done = false;
            _dialog.Show(script.speaker, text, label, alpha, () => done = true);
            while (!done) yield return null;
        }

        IEnumerator StatStep(string text, string meterName)
        {
            if (_phone != null) _phone.TutorialSpotlightMeter(meterName);
            yield return Say(text, script.continueLabel, statBackdropAlpha);
            if (_phone != null) _phone.ClearTutorial();
        }

        /// <summary>
        /// Manda un cliente scripteado hasta el frente de 'stallCell' y espera a que se venda, LLEGUE
        /// al puesto, cambie de ropa y camine un poco (para que se vea la venta / el robo).
        /// </summary>
        IEnumerator SendClient(Vector2Int stallCell, Vector2Int? entranceOverride = null)
        {
            var m = _grid.Model;
            var stall = m.GetOccupant(stallCell);
            if (stall == null) yield break;
            var front = m.FrontCell(stall.OriginCell, stall.Footprint, stall.Facing);
            var entrance = entranceOverride ?? NearestOpening(stallCell); // por defecto, cerca de ese puesto
            var exit = AnotherOpening(entrance);

            Client captured = null;
            while (captured == null)
            {
                var client = _waves.SpawnTutorialClient(entrance, front, exit, 2f);
                if (client == null) yield break; // no se pudo (mapa raro): no trabar el tutorial
                while (client != null && client.IsTargetable) yield return null;
                if (client != null && !client.IsTargetable) captured = client; // vendido
                // else null -> se fue sin comprar -> reintentar
            }
            while (captured != null && !captured.Arrived) yield return null; // llega al puesto (cambia de ropa)
            yield return new WaitForSeconds(1.4f);                            // compra + camina un poco
        }

        // ---- helpers de mapa ----

        bool IsPlayerStall(Vector2Int cell)
        {
            var occ = _grid != null && _grid.Model != null ? _grid.Model.GetOccupant(cell) : null;
            return occ != null && occ.Owner == Owner.Player;
        }

        static readonly Vector2Int[] Dirs4 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        /// <summary>Una celda de arena construible pegada a una boca (al lado de una puerta).</summary>
        Vector2Int FirstBuildCell()
        {
            var m = _grid.Model;
            foreach (var e in _waves.Openings)
                foreach (var d in Dirs4)
                {
                    var c = e + d;
                    if (m.InBounds(c) && m.GetCell(c) == CellType.Grass && m.GetOccupant(c) == null
                        && (_territory == null || _territory.CanBuild(Owner.Player, c, Vector2Int.one, out _)))
                        return c;
                }
            // fallback: primera construible que aparezca
            for (int x = 0; x < m.Width; x++)
                for (int y = 0; y < m.Height; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (m.GetCell(c) == CellType.Grass && m.GetOccupant(c) == null
                        && (_territory == null || _territory.CanBuild(Owner.Player, c, Vector2Int.one, out _)))
                        return c;
                }
            return _waves.Openings.Count > 0 ? _waves.Openings[0] : Vector2Int.zero;
        }

        /// <summary>Boca mas cercana a 'cell' (para que el cliente salga cerca de tu puesto y lo captes vos).</summary>
        Vector2Int NearestOpening(Vector2Int cell)
        {
            Vector2Int best = _waves.Openings.Count > 0 ? _waves.Openings[0] : Vector2Int.zero;
            int bestD = int.MaxValue;
            foreach (var o in _waves.Openings)
            {
                int d = Mathf.Abs(o.x - cell.x) + Mathf.Abs(o.y - cell.y);
                if (d < bestD) { bestD = d; best = o; }
            }
            return best;
        }

        Vector2Int AnotherOpening(Vector2Int notThis)
        {
            foreach (var o in _waves.Openings) if (o != notThis) return o;
            return notThis;
        }

        /// <summary>La boca de mas ARRIBA y, entre esas, la mas a la DERECHA.</summary>
        Vector2Int TopRightOpening()
        {
            Vector2Int best = _waves.Openings.Count > 0 ? _waves.Openings[0] : Vector2Int.zero;
            int bestScore = int.MinValue;
            foreach (var o in _waves.Openings)
            {
                int score = o.y * 1000 + o.x; // arriba manda; a igualdad, la mas a la derecha
                if (score > bestScore) { bestScore = score; best = o; }
            }
            return best;
        }

        /// <summary>Celda de arena libre en 'zone', con pasillo al lado, la mas cercana a 'near' (evita 'avoid').</summary>
        Vector2Int? FindCellInZone(char zone, Vector2Int near, HashSet<Vector2Int> avoid)
        {
            if (zone == '.') return null;
            var m = _grid.Model;
            Vector2Int? best = null; int bestD = int.MaxValue;
            for (int x = 0; x < m.Width; x++)
                for (int y = 0; y < m.Height; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (m.ZoneOf(c) != zone) continue;
                    if (m.GetCell(c) != CellType.Grass || m.GetOccupant(c) != null) continue;
                    if (avoid != null && avoid.Contains(c)) continue;
                    if (!HasAdjacentAisle(c)) continue;
                    int d = Mathf.Abs(c.x - near.x) + Mathf.Abs(c.y - near.y);
                    if (d < bestD) { bestD = d; best = c; }
                }
            return best;
        }

        bool HasAdjacentAisle(Vector2Int c)
        {
            var m = _grid.Model;
            foreach (var d in Dirs4) { var n = c + d; if (m.InBounds(n) && m.IsAisle(n)) return true; }
            return false;
        }

    }
}
