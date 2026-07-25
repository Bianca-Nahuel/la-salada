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
        [Tooltip("Clientes de la primera oleada (numero fijo, no aleatorio).")]
        public int baseClients = 12;
        [Tooltip("Clientes extra por cada oleada que pasa.")]
        public int perWaveIncrement = 3;
        [Tooltip("Duracion real de la oleada a x1 (las 4 horas). Los clientes se reparten en este tiempo.")]
        public float waveDuration = 45f;

        [Header("Clientes")]
        public float clientSpeed = 1.8f;
        public float convinceThreshold = 100f;
        public float clientSize = 0.5f;
        public Color clientColor = new Color(0.15f, 0.35f, 0.95f);
        public float buyPauseDuration = 1f;
        [Tooltip("Atencion que pierde un cliente por segundo si ningun puesto le pega.")]
        public float convinceDecayPerSec = 5f;

        [Header("Comportamiento de clientes")]
        [Tooltip("Prob. de que un cliente vaya rapido al centro y despues recorra lento (no solo campear salidas).")]
        [Range(0f, 1f)] public float centerRusherChance = 0.35f;
        [Tooltip("Prob. de que un cliente 'directo' tome el recorrido largo (si no, el corto).")]
        [Range(0f, 1f)] public float longPathChance = 0.45f;
        [Tooltip("Multiplicador de velocidad en el tramo rapido (hasta el centro).")]
        public float rushSpeedMult = 2.2f;
        [Tooltip("Multiplicador de velocidad en el tramo lento (despues del centro).")]
        public float slowSpeedMult = 0.55f;

        [Header("Economia")]
        public int startingMoney = 150;
        public int saleReward = 10;
        [Range(0f, 1f)] public float refundFraction = 0.5f;

        [Header("Dias / cuota")]
        [Tooltip("Cada cuantas oleadas pasa un dia.")]
        public int wavesPerDay = 3;
        [Tooltip("Hora en que arranca el dia (primera oleada).")]
        public int dayStartHour = 9;
        [Tooltip("Cuantas horas del juego dura cada oleada (9->13->17).")]
        public int hoursPerWave = 4;
        [Tooltip("Sueldo diario por cada puesto propio (empleados).")]
        public int salaryPerStall = 5;
        [Tooltip("Cuota de proteccion base por dia.")]
        public int protectionBase = 30;
        [Tooltip("Cuanto sube la proteccion por cada dia que pasa.")]
        public int protectionPerDay = 10;

        [System.Serializable]
        public class ExpansionTuning
        {
            [Tooltip("Ventas necesarias para la PRIMERA expansion.")] public int baseCost = 3;
            [Tooltip("Cuantas ventas mas cuesta cada expansion siguiente (cada vez cuesta mas).")] public int growth = 2;
        }

        [Header("Expansion de facciones (al terminar la oleada)")]
        [Tooltip("Ritmo de expansion del rival ROJO (Enemy).")]
        public ExpansionTuning enemyExpansion = new ExpansionTuning();
        [Tooltip("Ritmo de expansion del rival AMARILLO (Neutral).")]
        public ExpansionTuning neutralExpansion = new ExpansionTuning();
        public StallData expansionStallData;

        ExpansionTuning TuningFor(Owner o) => o == Owner.Enemy ? enemyExpansion : neutralExpansion;

        [Header("Balanzas por disputa")]
        public float reputationPerSale = 0.5f;   // ganar una venta sube reputacion
        public float hostilityPerSale = 0.3f;    // ganarle una venta al rival sube su hostilidad
        public float reputationPerEscape = 0.5f; // un cliente que se va sin comprar baja reputacion

        public int Money { get; private set; }
        public int Wave { get; private set; }
        public int Day { get; private set; } = 1;
        /// <summary>Oleadas ya completadas en el dia actual (0..wavesPerDay).</summary>
        public int WavesToday { get; private set; }
        public int SalesWon { get; private set; }
        public int SalesLost { get; private set; }
        public int Escaped { get; private set; }
        public Phase CurrentPhase { get; private set; } = Phase.Building;
        public bool IsBuilding => CurrentPhase == Phase.Building;

        /// <summary>Ya se jugaron todas las oleadas del dia: solo queda avanzar de dia (manual).</summary>
        public bool DayComplete => CurrentPhase == Phase.Building && WavesToday >= wavesPerDay;

        private float _waveElapsed;

        /// <summary>Progreso [0..1] de la oleada actual (por tiempo). 0 si no hay oleada.</summary>
        public float WaveProgress => CurrentPhase == Phase.WaveActive
            ? Mathf.Clamp01(_waveElapsed / Mathf.Max(0.01f, waveDuration)) : 0f;

        /// <summary>
        /// Hora del juego a mostrar. En Building = hora de la proxima oleada (9/13/17, o fin de
        /// dia). Durante la oleada avanza parejo con el tiempo hasta llegar justo a la proxima hora.
        /// </summary>
        public float DisplayHour => HourFor(WavesToday, WaveProgress);

        /// <summary>Hora del juego dado cuantas oleadas van jugadas y el progreso [0..1] de la actual.</summary>
        public float HourFor(int wavesDone, float progress01)
            => dayStartHour + hoursPerWave * (Mathf.Clamp(wavesDone, 0, wavesPerDay) + Mathf.Clamp01(progress01));

        /// <summary>El reloj como texto "H:MM" (ej "9:00", "13:00").</summary>
        public string ClockText
        {
            get
            {
                float h = DisplayHour;
                int hh = Mathf.FloorToInt(h);
                int mm = Mathf.FloorToInt((h - hh) * 60f);
                return $"{hh}:{mm:00}";
            }
        }

        /// <summary>Se dispara al avanzar de dia (manual). Lo usa el EventManager.</summary>
        public event System.Action DayPassed;

        private readonly Dictionary<Owner, int> _pendingExpansion = new Dictionary<Owner, int>();
        private readonly Dictionary<Owner, int> _expansionsDone = new Dictionary<Owner, int>();
        private List<Vector2Int> _openings;
        private Transform _clientsParent;
        private int _rng = 12345;
        private BusinessMeters _meters;
        private GameEffects _effects;
        private Salada.Game.TerritoryManager _territory;

        void Start()
        {
            if (grid == null) grid = FindAnyObjectByType<GridManager>();
            if (grid.Model == null) grid.BuildModel();
            _meters = FindAnyObjectByType<BusinessMeters>();
            _effects = FindAnyObjectByType<GameEffects>();
            _territory = FindAnyObjectByType<Salada.Game.TerritoryManager>();
            _openings = grid.Model.EntranceCells.Count > 0
                ? new List<Vector2Int>(grid.Model.EntranceCells)
                : grid.Model.GetBorderAisleOpenings();
            _clientsParent = new GameObject("Clients").transform;
            Money = startingMoney;
        }

        public void AddMoney(int amount) => Money += amount; // eventos (permite negativo)

        void Update()
        {
            if (CurrentPhase == Phase.WaveActive) _waveElapsed += Time.deltaTime; // el reloj corre (mas rapido a mayor velocidad)
        }

        // ---- Fase / oleadas ----

        /// <summary>Arranca la proxima oleada del dia. Si el dia ya se completo, no hace nada (hay que avanzar de dia).</summary>
        public void StartWave()
        {
            if (CurrentPhase == Phase.WaveActive) return;
            if (WavesToday >= wavesPerDay) return; // dia completo -> AdvanceDay()
            if (_openings == null || _openings.Count < 2) { Debug.LogError("[WaveManager] Sin bocas suficientes."); return; }
            StartCoroutine(RunOneWave());
        }

        IEnumerator RunOneWave()
        {
            CurrentPhase = Phase.WaveActive;
            Wave++;
            _waveElapsed = 0f;
            int baseCount = baseClients + perWaveIncrement * (Wave - 1);
            int count = Mathf.Max(0, Mathf.RoundToInt(baseCount * (_effects != null ? _effects.ClientCountMult : 1f)));

            // Oleada por tiempo: los 'count' clientes (numero fijo) se reparten parejo a lo largo
            // de waveDuration. El cliente i entra cuando el progreso alcanza (i+0.5)/count.
            int spawned = 0;
            while (_waveElapsed < waveDuration)
            {
                while (spawned < count && (spawned + 0.5f) / count <= _waveElapsed / waveDuration)
                {
                    SpawnClient();
                    spawned++;
                }
                yield return null;
            }
            while (spawned < count) { SpawnClient(); spawned++; } // por si quedo alguno pendiente
            while (Client.Active.Count > 0) yield return null;    // dejar que terminen de salir

            if (_effects == null || !_effects.ExpansionBlocked) EndOfWaveExpansion();
            if (_effects != null) _effects.OnWaveEnded();       // vencer efectos de esta oleada
            WavesToday++;                                        // una oleada mas del dia jugada
            CurrentPhase = Phase.Building;
        }

        /// <summary>
        /// Avanza al dia siguiente (manual: nunca automatico). Solo cuando ya se jugaron
        /// todas las oleadas del dia. Cobra la cuota del dia que cierra y dispara el evento diario.
        /// </summary>
        public void AdvanceDay()
        {
            if (!DayComplete) return;
            ChargeDailyFee();     // cobra la cuota del dia que se cierra
            Day++;
            WavesToday = 0;
            _waveElapsed = 0f;
            DayPassed?.Invoke();  // evento del nuevo dia
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
            var start = _openings[NextInt(_openings.Count)];
            List<Vector2Int> cells;
            int rushUntil = 0;
            bool rusher = _openings.Count >= 2 && NextFloat() < centerRusherChance;

            if (rusher)
            {
                // va rapido al centro y despues recorre lento hasta una salida (no solo campear salidas)
                var center = grid.Model.NearestAisleCell(MapCenterWorld());
                var exit = PickDifferentOpening(start);
                var p1 = grid.Model.FindAislePath(start, center);
                var p2 = grid.Model.FindAislePath(center, exit);
                if (p1 == null || p2 == null || p1.Count < 1) { rusher = false; cells = DirectPath(start); }
                else
                {
                    cells = new List<Vector2Int>(p1);
                    for (int i = 1; i < p2.Count; i++) cells.Add(p2[i]);
                    rushUntil = p1.Count; // waypoints hasta el centro = tramo rapido
                }
            }
            else cells = DirectPath(start);

            if (cells == null || cells.Count < 2) return;

            var worldPath = new List<Vector3>(cells.Count);
            foreach (var c in cells) { var wc = grid.Model.CellToWorldCenter(c); worldPath.Add(new Vector3(wc.x, wc.y, 0f)); }

            var go = new GameObject("Client");
            go.transform.SetParent(_clientsParent, false);
            var client = go.AddComponent<Client>();
            client.Init(grid, worldPath, clientSpeed, convinceThreshold, buyPauseDuration, clientSize, clientColor,
                convinceDecayPerSec, OnConverted, OnEscaped);
            if (rusher) client.SetRush(rushUntil, clientSpeed * rushSpeedMult, clientSpeed * slowSpeedMult);
        }

        /// <summary>Genera un cliente ya mismo (para testeo).</summary>
        public void DebugSpawnClient() => SpawnClient();

        /// <summary>Recorrido directo boca->boca, corto o largo (segun longPathChance).</summary>
        List<Vector2Int> DirectPath(Vector2Int start)
        {
            bool wantLong = NextFloat() < longPathChance;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                var goal = PickGoal(start, wantLong);
                if (goal == start) continue;
                var path = grid.Model.FindAislePath(start, goal);
                if (path != null && path.Count >= 2) return path;
            }
            return null;
        }

        /// <summary>Boca destino distinta a start: la mas lejana (largo) o la mas cercana (corto), con algo de azar.</summary>
        Vector2Int PickGoal(Vector2Int start, bool wantLong)
        {
            Vector2Int best = start; int bestD = wantLong ? -1 : int.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                var o = _openings[NextInt(_openings.Count)];
                if (o == start) continue;
                int d = Manhattan(o, start);
                if (wantLong ? d > bestD : d < bestD) { bestD = d; best = o; }
            }
            return best == start ? PickDifferentOpening(start) : best;
        }

        Vector2Int PickDifferentOpening(Vector2Int start)
        {
            for (int i = 0; i < 12; i++) { var o = _openings[NextInt(_openings.Count)]; if (o != start) return o; }
            return start;
        }

        Vector2 MapCenterWorld()
        {
            var m = grid.Model;
            return new Vector2(m.Origin.x + m.Width * m.CellSize * 0.5f, m.Origin.y + m.Height * m.CellSize * 0.5f);
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
                RegisterSteals(c);
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

        /// <summary>
        /// Robo de venta: si el jugador gano un cliente que un rival tambien estaba convenciendo,
        /// en una zona disputada con ese rival, baja la paciencia de esa disputa.
        /// </summary>
        void RegisterSteals(Client c)
        {
            if (_territory == null || c.LastSaleStall == null) return;
            char zone = grid.Model.ZoneOf(c.LastSaleStall.OriginCell);
            if (zone == '.') return;
            foreach (var rival in new[] { Owner.Neutral, Owner.Enemy })
                if (c.ConvinceBy(rival) > 0f)
                    _territory.RegisterSteal(zone, Owner.Player, rival);
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
            var t = TuningFor(owner);
            _pendingExpansion.TryGetValue(owner, out int n);
            _expansionsDone.TryGetValue(owner, out int done);
            // cada expansion cuesta mas ventas que la anterior (ritmo por faccion)
            int safety = 0;
            while (safety++ < 200)
            {
                int need = Mathf.Max(1, t.baseCost + t.growth * done); // nunca 0 -> evita loop infinito
                if (n < need) break;
                if (!ExpandFaction(owner)) break; // sin celda valida -> no gastar las ventas
                n -= need;
                done++;
            }
            _pendingExpansion[owner] = n;
            _expansionsDone[owner] = done;
        }

        bool ExpandFaction(Owner owner)
        {
            var cell = FindExpansionCell(owner);
            if (!cell.HasValue) return false;
            var center = grid.Model.FootprintCenterWorld(cell.Value, Vector2Int.one);
            var facing = grid.Model.FacingToAisle(cell.Value);
            var stall = grid.SpawnStall(cell.Value, Vector2Int.one, owner, facing, expansionStallData);
            if (stall == null) return false;
            FloatingText.Spawn(center, owner == Owner.Enemy ? "Rival rojo +1" : "Rival amarillo +1", grid.ColorFor(owner));
            return true;
        }

        public string DebugExpansionInfo(Owner o)
        {
            var t = TuningFor(o);
            _pendingExpansion.TryGetValue(o, out int n);
            _expansionsDone.TryGetValue(o, out int d);
            return $"{o}: pending={n} expansiones={d} proxCosto={t.baseCost + t.growth * d}";
        }

        /// <summary>Agrega 'n' ventas pendientes a la faccion y corre la expansion (para testeo).</summary>
        public void DebugFeedSales(Owner o, int n)
        {
            _pendingExpansion.TryGetValue(o, out int cur);
            _pendingExpansion[o] = cur + n;
            TryExpand(o);
        }

        /// <summary>Fuerza una expansion de la faccion (para testeo).</summary>
        public bool DebugExpand(Owner owner)
        {
            var cell = FindExpansionCell(owner);
            if (!cell.HasValue) return false;
            var facing = grid.Model.FacingToAisle(cell.Value);
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
                    if (m.GetCell(cell) != CellType.Grass || m.GetOccupant(cell) != null) continue;
                    // los rivales tambien respetan las zonas: solo su zona o adyacente (y no meterse en disputa ajena)
                    if (_territory != null && !_territory.CanBuild(owner, cell, Vector2Int.one, out _)) continue;
                    candidates.Add(cell);
                }
            if (candidates.Count == 0) return null;

            // Direccion segun TU hostilidad: es la probabilidad de que se ACERQUEN a vos.
            // Ej: hostilidad 20 -> 20% de acercarse, 80% de alejarse. (Vale para rojo y amarillo.)
            bool playerHasStalls = false;
            foreach (var s in m.Stalls) if (s.Owner == Owner.Player) { playerHasStalls = true; break; }
            if (playerHasStalls)
            {
                float hostChance = _meters != null ? _meters.HostilityChance : 0.5f;
                bool approach = NextFloat() < hostChance;
                var pick = approach ? NearestCandidateTo(candidates, Owner.Player)
                                    : FarthestCandidateFrom(candidates, Owner.Player);
                if (pick.HasValue) return pick.Value;
            }

            // Sin puestos del jugador todavia: se agrupan cerca de lo propio.
            var nearOwn = new List<Vector2Int>();
            foreach (var cand in candidates)
                foreach (var s in m.Stalls)
                    if (s.Owner == owner && Manhattan(cand, s.OriginCell) <= 2) { nearOwn.Add(cand); break; }
            if (nearOwn.Count > 0) return nearOwn[NextInt(nearOwn.Count)];
            return candidates[NextInt(candidates.Count)];
        }

        /// <summary>Candidato mas LEJANO a cualquier puesto de 'target' (para alejarse); null si no hay.</summary>
        Vector2Int? FarthestCandidateFrom(List<Vector2Int> candidates, Owner target)
        {
            Vector2Int? best = null;
            int bestD = -1;
            foreach (var cand in candidates)
            {
                int nearest = int.MaxValue;
                foreach (var s in grid.Model.Stalls)
                    if (s.Owner == target) nearest = Mathf.Min(nearest, Manhattan(cand, s.OriginCell));
                if (nearest != int.MaxValue && nearest > bestD) { bestD = nearest; best = cand; }
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
