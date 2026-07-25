using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Salada.Placement;
using Salada.Game;

namespace Salada.Combat
{
    /// <summary>
    /// Ritmo del juego por fases: en Building el jugador construye/demuele; el jugador
    /// dispara cada oleada con StartWave(). Durante la oleada no se construye. Al terminar
    /// la oleada, las facciones rival/neutral se expanden segun sus ventas. Lleva la economia.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public enum Phase { Building, WaveActive }

        [SerializeField] private GridManager grid;

        [Header("Oleadas")]
        public int baseClients = 4;
        public int perWaveIncrement = 1;
        public float spawnInterval = 1.3f;

        [Header("Clientes")]
        public float clientSpeed = 1.8f;
        public float convinceThreshold = 100f;
        public float clientSize = 0.5f;
        public Color clientColor = new Color(0.15f, 0.35f, 0.95f);
        public float buyPauseDuration = 1f;

        [Header("Economia")]
        public int startingMoney = 150;
        public int saleReward = 10;
        [Range(0f, 1f)] public float refundFraction = 0.5f;

        [Header("Dias / cuota")]
        [Tooltip("Cada cuantas oleadas pasa un dia.")]
        public int wavesPerDay = 3;
        [Tooltip("Sueldo diario por cada puesto propio (empleados).")]
        public int salaryPerStall = 5;
        [Tooltip("Cuota de proteccion base por dia.")]
        public int protectionBase = 30;
        [Tooltip("Cuanto sube la proteccion por cada dia que pasa.")]
        public int protectionPerDay = 10;

        [Header("Expansion de facciones (al terminar la oleada)")]
        public int expandEvery = 3;
        public StallData expansionStallData;

        [Header("Balanzas por disputa")]
        public float reputationPerSale = 0.5f;   // ganar una venta sube reputacion
        public float hostilityPerSale = 0.3f;    // ganarle una venta al rival sube su hostilidad
        public float reputationPerEscape = 0.5f; // un cliente que se va sin comprar baja reputacion

        public int Money { get; private set; }
        public int Wave { get; private set; }
        public int Day { get; private set; } = 1;
        public int SalesWon { get; private set; }
        public int SalesLost { get; private set; }
        public int Escaped { get; private set; }
        public Phase CurrentPhase { get; private set; } = Phase.Building;
        public bool IsBuilding => CurrentPhase == Phase.Building;

        /// <summary>Se dispara cuando pasa un dia (cada wavesPerDay oleadas). Lo usa el EventManager.</summary>
        public event System.Action DayPassed;

        private readonly Dictionary<Owner, int> _pendingExpansion = new Dictionary<Owner, int>();
        private List<Vector2Int> _openings;
        private Transform _clientsParent;
        private int _rng = 12345;
        private BusinessMeters _meters;
        private GameEffects _effects;

        void Start()
        {
            if (grid == null) grid = FindFirstObjectByType<GridManager>();
            if (grid.Model == null) grid.BuildModel();
            _meters = FindFirstObjectByType<BusinessMeters>();
            _effects = FindFirstObjectByType<GameEffects>();
            _openings = grid.Model.EntranceCells.Count > 0
                ? new List<Vector2Int>(grid.Model.EntranceCells)
                : grid.Model.GetBorderAisleOpenings();
            _clientsParent = new GameObject("Clients").transform;
            Money = startingMoney;
        }

        public void AddMoney(int amount) => Money += amount; // eventos (permite negativo)

        // ---- Fase / oleadas ----

        /// <summary>Arranca la proxima oleada (solo si estamos en Building).</summary>
        public void StartWave()
        {
            if (CurrentPhase == Phase.WaveActive) return;
            if (_openings == null || _openings.Count < 2) { Debug.LogError("[WaveManager] Sin bocas suficientes."); return; }
            StartCoroutine(RunOneWave());
        }

        IEnumerator RunOneWave()
        {
            CurrentPhase = Phase.WaveActive;
            Wave++;
            int baseCount = baseClients + perWaveIncrement * (Wave - 1);
            int count = Mathf.Max(0, Mathf.RoundToInt(baseCount * (_effects != null ? _effects.ClientCountMult : 1f)));
            for (int i = 0; i < count; i++)
            {
                SpawnClient();
                yield return new WaitForSeconds(spawnInterval);
            }
            while (Client.Active.Count > 0) yield return null;

            if (_effects == null || !_effects.ExpansionBlocked) EndOfWaveExpansion();
            bool dayPassed = (Wave % wavesPerDay == 0);
            if (dayPassed) { ChargeDailyFee(); Day++; }        // paso un dia -> cobrar cuota
            if (_effects != null) _effects.OnWaveEnded();       // vencer efectos de esta oleada
            CurrentPhase = Phase.Building;
            if (dayPassed) DayPassed?.Invoke();                 // disparar el evento del dia
        }

        // ---- Dias / cuota ----

        public int PlayerStallCount()
        {
            if (grid == null || grid.Model == null) return 0;
            int c = 0;
            foreach (var s in grid.Model.Stalls) if (s.Owner == Owner.Player) c++;
            return c;
        }

        int ProtectionFor(int day) => protectionBase + protectionPerDay * (day - 1);

        /// <summary>Cuota del dia actual: sueldos (por puesto) + proteccion (sube con los dias).</summary>
        public int DailyFee() => salaryPerStall * PlayerStallCount() + ProtectionFor(Day);

        void ChargeDailyFee()
        {
            int fee = DailyFee();
            Money -= fee; // puede quedar en negativo (deuda)
            var center = grid.Model.FootprintCenterWorld(Vector2Int.zero, new Vector2Int(grid.Model.Width, grid.Model.Height));
            FloatingText.Spawn(center, $"Dia {Day}: -${fee}", new Color(1f, 0.4f, 0.3f));
        }

        // ---- Economia ----

        public bool CanAfford(int cost) => Money >= cost;
        public void Spend(int cost) => Money = Mathf.Max(0, Money - cost);
        public void RefundStall(int cost) => Money += Mathf.RoundToInt(cost * refundFraction);

        // ---- Clientes ----

        void SpawnClient()
        {
            List<Vector2Int> path = null;
            for (int attempt = 0; attempt < 12 && path == null; attempt++)
            {
                var start = _openings[NextInt(_openings.Count)];
                var goal = _openings[NextInt(_openings.Count)];
                if (goal == start) continue;
                path = grid.Model.FindAislePath(start, goal);
            }
            if (path == null || path.Count < 2) return;

            var worldPath = new List<Vector3>(path.Count);
            foreach (var c in path) { var wc = grid.Model.CellToWorldCenter(c); worldPath.Add(new Vector3(wc.x, wc.y, 0f)); }

            var go = new GameObject("Client");
            go.transform.SetParent(_clientsParent, false);
            var client = go.AddComponent<Client>();
            client.Init(grid, worldPath, clientSpeed, convinceThreshold, buyPauseDuration, clientSize, clientColor,
                OnConverted, OnEscaped);
        }

        void OnConverted(Client c, Owner winner)
        {
            if (winner == Owner.Player)
            {
                float mult = (_meters != null ? _meters.SaleRewardMult : 1f) * (_effects != null ? _effects.SaleRewardMult : 1f);
                int reward = Mathf.RoundToInt(saleReward * mult); // profit + efectos
                Money += reward;
                SalesWon++;
                FloatingText.Spawn(c.transform.position, "+$" + reward, grid.playerColor);
                if (_meters != null)
                {
                    _meters.Add(MeterType.Reputation, reputationPerSale);  // ganar sube reputacion
                    _meters.Add(MeterType.Hostility, hostilityPerSale);   // le sacamos ventas al rival -> nos odia mas
                }
            }
            else
            {
                SalesLost++;
                _pendingExpansion.TryGetValue(winner, out int n); // expansion diferida al fin de la oleada
                _pendingExpansion[winner] = n + 1;
            }
        }

        void OnEscaped(Client c)
        {
            Escaped++;
            if (_meters != null) _meters.Add(MeterType.Reputation, -reputationPerEscape); // mal servicio
        }

        // ---- Expansion al terminar la oleada ----

        void EndOfWaveExpansion()
        {
            TryExpand(Owner.Neutral);
            TryExpand(Owner.Enemy);
        }

        void TryExpand(Owner owner)
        {
            if (expansionStallData == null) return;
            _pendingExpansion.TryGetValue(owner, out int n);
            while (n >= expandEvery) { ExpandFaction(owner); n -= expandEvery; }
            _pendingExpansion[owner] = n;
        }

        void ExpandFaction(Owner owner)
        {
            var cell = FindExpansionCell(owner);
            if (!cell.HasValue) return;
            var center = grid.Model.FootprintCenterWorld(cell.Value, Vector2Int.one);
            var facing = grid.Model.NearestAisleDirection(center);
            var stall = grid.SpawnStall(cell.Value, Vector2Int.one, owner, facing, expansionStallData);
            if (stall != null)
                FloatingText.Spawn(center, owner == Owner.Enemy ? "Rival rojo +1" : "Rival amarillo +1", grid.ColorFor(owner));
        }

        /// <summary>Fuerza una expansion de la faccion (para testeo).</summary>
        public bool DebugExpand(Owner owner)
        {
            var cell = FindExpansionCell(owner);
            if (!cell.HasValue) return false;
            var facing = grid.Model.NearestAisleDirection(grid.Model.FootprintCenterWorld(cell.Value, Vector2Int.one));
            return grid.SpawnStall(cell.Value, Vector2Int.one, owner, facing, expansionStallData) != null;
        }

        Vector2Int? FindExpansionCell(Owner owner)
        {
            var m = grid.Model;
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < m.Width; x++)
                for (int y = 0; y < m.Height; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (m.GetCell(cell) == CellType.Grass && m.GetOccupant(cell) == null)
                        candidates.Add(cell);
                }
            if (candidates.Count == 0) return null;

            // Hostilidad: los rivales tienden a expandirse cerca de NUESTROS puestos.
            if (owner == Owner.Enemy && _meters != null && NextFloat() < _meters.HostilityChance)
            {
                var hostile = NearestCandidateTo(candidates, Owner.Player);
                if (hostile.HasValue) return hostile.Value;
            }

            var nearOwn = new List<Vector2Int>();
            foreach (var cand in candidates)
                foreach (var s in m.Stalls)
                    if (s.Owner == owner && Manhattan(cand, s.OriginCell) <= 2) { nearOwn.Add(cand); break; }
            if (nearOwn.Count > 0) return nearOwn[NextInt(nearOwn.Count)];

            Vector2Int best = candidates[0];
            float bestDist = -1f;
            foreach (var cand in candidates)
            {
                float nearest = float.MaxValue;
                foreach (var s in m.Stalls)
                    if (s.Owner != owner) nearest = Mathf.Min(nearest, Manhattan(cand, s.OriginCell));
                if (nearest > bestDist) { bestDist = nearest; best = cand; }
            }
            return best;
        }

        /// <summary>Candidato mas cercano a cualquier puesto de 'target'; null si no hay puestos de esa faccion.</summary>
        Vector2Int? NearestCandidateTo(List<Vector2Int> candidates, Owner target)
        {
            Vector2Int? best = null;
            int bestD = int.MaxValue;
            foreach (var cand in candidates)
            {
                int nearest = int.MaxValue;
                foreach (var s in grid.Model.Stalls)
                    if (s.Owner == target) nearest = Mathf.Min(nearest, Manhattan(cand, s.OriginCell));
                if (nearest < bestD) { bestD = nearest; best = cand; }
            }
            return best;
        }

        static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        int NextInt(int max)
        {
            _rng = _rng * 1103515245 + 12345;
            int v = (_rng >> 16) & 0x7fff;
            return max <= 0 ? 0 : v % max;
        }

        float NextFloat() => NextInt(10000) / 10000f;
    }
}
