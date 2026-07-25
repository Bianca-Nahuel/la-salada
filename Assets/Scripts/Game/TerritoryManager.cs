using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Salada.Placement;
using Salada.Combat;
using Salada.UI;

namespace Salada.Game
{
    public enum ZoneState { Neutral, Owned, Disputed }

    /// <summary>
    /// Modulo de disputas de territorio (Etapa 1). El mapa se divide en zonas pintadas; cada
    /// zona es neutral / propia de una faccion / disputada (dos facciones). Calcula el poder por
    /// zona, restringe la construccion (zona propia o adyacente), lleva una "paciencia" OCULTA
    /// por zona disputada con el jugador (decae por dia/hostilidad/robos/diferencia de poder) y
    /// resuelve las disputas con un minijuego tira-y-afloja, destruyendo puestos del perdedor
    /// de a poco segun el margen. Antes de un ataque sorpresa avisa cualitativamente.
    /// </summary>
    public class TerritoryManager : MonoBehaviour
    {
        public struct ZoneStatus
        {
            public char zone;
            public ZoneState state;
            public Owner ownerA; public int countA; // dueño principal (Owned/Disputed)
            public Owner ownerB; public int countB; // segundo dueño (Disputed)
        }

        [Header("Zona inicial del jugador (fallback si el mapa no la define)")]
        [SerializeField] private string playerHomeZone = "a";

        [Header("Expansion (ritmo)")]
        [Tooltip("Puestos que necesitas en tu zona inicial para poder construir en zonas adyacentes a ella.")]
        [SerializeField] private int firstRingStalls = 1;
        [Tooltip("Puestos tuyos que necesitas en las zonas adyacentes a una zona (mas lejana) para abrirla.")]
        [SerializeField] private int outerRingStalls = 2;
        [Tooltip("Puestos que un rival necesita en zonas adyacentes para expandirse a una nueva (mismo limite que vos).")]
        [SerializeField] private int rivalExpandStalls = 2;

        [Header("Poder")]
        [Tooltip("Normaliza la diferencia de poder en las formulas de paciencia/dureza.")]
        [SerializeField] private float powerScale = 4f;

        [Header("Paciencia (oculta)")]
        [SerializeField] private float patienceMax = 100f;
        [SerializeField] private float dailyDecayBase = 6f;
        [SerializeField] private float warnThreshold = 30f;
        [SerializeField] private float attackThreshold = 0f;
        [SerializeField] private float stealDecay = 4f;
        [SerializeField] private float powerGapK = 0.6f;

        [Header("Negociar / Atacar")]
        [SerializeField] private int negotiateBaseCost = 40;
        [SerializeField] private float attackReputationCost = 8f;
        [SerializeField] private float attackHostilityGain = 8f;
        [SerializeField] private int maxStallLoss = 3;

        [Header("Disputas rival-vs-rival (Etapa 2)")]
        [Tooltip("Probabilidad por dia de que dos rivales peleen en una zona que se disputan entre ellos.")]
        [SerializeField] private float rivalClashChance = 0.5f;
        [Tooltip("Cuantos puestos pierde el perdedor por pelea (de a poco: guerra de desgaste).")]
        [SerializeField] private int rivalClashStallLoss = 1;

        [SerializeField] private Color warningColor = new Color(1f, 0.5f, 0.2f);

        private GridManager _grid;
        private WaveManager _waves;
        private BusinessMeters _meters;
        private EventManager _events;
        private DisputeMinigame _minigame;
        private WarningPopup _warningPopup;

        private readonly Dictionary<char, float> _patience = new Dictionary<char, float>();
        private readonly HashSet<char> _warned = new HashSet<char>();
        private readonly List<char> _pendingAttacks = new List<char>();
        private bool _draining;
        private int _rng = 24680;

        float NextFloat() { _rng = _rng * 1103515245 + 12345; return ((_rng >> 16) & 0x7fff) / 32767f; }

        /// <summary>Aviso cualitativo (zona, mensaje). Hook para una futura app de "Mensajes".</summary>
        public event Action<char, string> ZoneTense;

        private char HomeZone
        {
            get
            {
                // prioridad: la zona home autorada en el MapLayout; si no, el fallback serializado
                var layout = _grid != null ? _grid.Layout : null;
                if (layout != null && layout.playerHomeZone != '.') return layout.playerHomeZone;
                return string.IsNullOrEmpty(playerHomeZone) ? '.' : playerHomeZone[0];
            }
        }
        private GridModel Model => _grid != null ? _grid.Model : null;

        void Start()
        {
            if (_grid == null) _grid = FindAnyObjectByType<GridManager>();
            _waves = FindAnyObjectByType<WaveManager>();
            _meters = FindAnyObjectByType<BusinessMeters>();
            _events = FindAnyObjectByType<EventManager>();
            _minigame = FindAnyObjectByType<DisputeMinigame>(FindObjectsInactive.Include);
            _warningPopup = FindAnyObjectByType<WarningPopup>(FindObjectsInactive.Include);
            if (_waves != null) _waves.DayPassed += OnDayPassed;
        }

        void OnDestroy()
        {
            if (_waves != null) _waves.DayPassed -= OnDayPassed;
        }

        // ---- Estado de zona ----

        public ZoneStatus StatusOf(char zone)
        {
            var st = new ZoneStatus { zone = zone };
            if (Model == null || zone == '.') return st;

            var present = new List<(Owner o, int c)>();
            foreach (var o in new[] { Owner.Player, Owner.Neutral, Owner.Enemy })
            {
                int c = Model.StallCountInZone(zone, o);
                if (c > 0) present.Add((o, c));
            }
            if (present.Count == 0) { st.state = ZoneState.Neutral; return st; }
            if (present.Count == 1) { st.state = ZoneState.Owned; st.ownerA = present[0].o; st.countA = present[0].c; return st; }

            present.Sort((a, b) => b.c.CompareTo(a.c));
            if (present.Count >= 3)
                Debug.LogWarning($"[Territory] Zona '{zone}' con {present.Count} facciones (no deberia pasar).");
            st.state = ZoneState.Disputed;
            st.ownerA = present[0].o; st.countA = present[0].c;
            st.ownerB = present[1].o; st.countB = present[1].c;
            return st;
        }

        public bool IsOwnZone(char zone, Owner f)
        {
            var st = StatusOf(zone);
            return st.state == ZoneState.Owned && st.ownerA == f;
        }

        /// <summary>Categoria de construccion de una zona para una faccion (para el overlay visual).</summary>
        public enum BuildCategory { Own, AdjacentFree, AdjacentDisputed, Blocked }

        public BuildCategory ZoneBuildCategory(Owner f, char zone)
        {
            if (Model == null || zone == '.') return BuildCategory.Blocked;
            var st = StatusOf(zone);
            bool thirdBlocked = st.state == ZoneState.Disputed && st.ownerA != f && st.ownerB != f;
            if (thirdBlocked || !IsBuildableZone(f, zone)) return BuildCategory.Blocked;

            foreach (var seed in BuildableSeedZones(f))
                if (seed == zone) return BuildCategory.Own; // zona propia (home o con puestos mios)

            // zona adyacente donde puedo construir: si ya hay un rival (Owned/Disputed) construir ahi
            // arranca/entra en disputa -> amarillo; si esta vacia (Neutral) es expansion tranquila -> verde.
            return st.state == ZoneState.Neutral ? BuildCategory.AdjacentFree : BuildCategory.AdjacentDisputed;
        }

        static bool IsDisputant(ZoneStatus st, Owner f) =>
            st.state == ZoneState.Disputed && (st.ownerA == f || st.ownerB == f);

        static Owner Rival(ZoneStatus st) => st.ownerA == Owner.Player ? st.ownerB : st.ownerA;

        // ---- Construccion: zona propia o adyacente ----

        public bool CanBuild(Owner f, Vector2Int origin, Vector2Int footprint, out string reason)
        {
            if (Model == null) { reason = "Sin modelo"; return false; }
            if (!Model.CanPlace(origin, footprint, out reason)) return false;

            char z = Model.ZoneOf(origin);
            if (z == '.') { reason = "Fuera de zona"; return false; }
            for (int dx = 0; dx < footprint.x; dx++)
                for (int dy = 0; dy < footprint.y; dy++)
                    if (Model.ZoneOf(new Vector2Int(origin.x + dx, origin.y + dy)) != z)
                    { reason = "El puesto cruza zonas"; return false; }

            var st = StatusOf(z);
            if (st.state == ZoneState.Disputed && st.ownerA != f && st.ownerB != f)
            { reason = "Zona ya en disputa por otras dos facciones"; return false; }

            if (!IsBuildableZone(f, z)) { reason = "Zona no adyacente a tu territorio"; return false; }
            reason = null;
            return true;
        }

        IEnumerable<char> BuildableSeedZones(Owner f)
        {
            var seeds = new HashSet<char>();
            foreach (var s in Model.Stalls)
                if (s.Owner == f) { char z = Model.ZoneOf(s.OriginCell); if (z != '.') seeds.Add(z); }
            if (f == Owner.Player && HomeZone != '.') seeds.Add(HomeZone);
            return seeds;
        }

        bool IsBuildableZone(Owner f, char z)
        {
            if (z == '.') return false;

            // zonas donde ya construyo (home o donde ya tengo puestos): siempre
            foreach (var seed in BuildableSeedZones(f))
                if (seed == z) return true;

            // zona nueva: hace falta un minimo de puestos MIOS en las zonas adyacentes a z
            int adjStalls = 0;
            foreach (var w in Model.ZoneNeighbors(z))
                adjStalls += Model.StallCountInZone(w, f);

            return adjStalls >= ExpandRequirement(f, z);
        }

        /// <summary>Puestos que 'f' necesita en las zonas adyacentes para abrir la zona z.</summary>
        int ExpandRequirement(Owner f, char z)
        {
            if (f != Owner.Player) return rivalExpandStalls;
            char home = HomeZone;
            // primera expansion (zonas pegadas a la inicial) es mas barata que las mas lejanas
            return (home != '.' && Model.ZoneDistance(home, z) == 1) ? firstRingStalls : outerRingStalls;
        }

        // ---- Poder ----

        public int PowerOf(char zone, Owner f)
        {
            if (Model == null) return 0;
            int power = 2 * Model.StallCountInZone(zone, f);
            foreach (var oz in Model.Zones)
            {
                if (oz == zone || !IsOwnZone(oz, f)) continue;
                int d = Model.ZoneDistance(zone, oz);
                if (d == 1) power += 2;
                else if (d == 2) power += 1;
            }
            return power;
        }

        public int PowerGap(char zone, Owner a, Owner b) => PowerOf(zone, a) - PowerOf(zone, b);

        // ---- Paciencia (oculta) ----

        void ReconcilePatienceEntries()
        {
            if (Model == null) return;
            foreach (var zone in Model.Zones)
            {
                var st = StatusOf(zone);
                bool playerDisputed = IsDisputant(st, Owner.Player);
                if (playerDisputed) { if (!_patience.ContainsKey(zone)) _patience[zone] = patienceMax; }
                else { _patience.Remove(zone); _warned.Remove(zone); _pendingAttacks.Remove(zone); }
            }
        }

        void OnDayPassed()
        {
            if (Model == null) return;
            ReconcilePatienceEntries();
            float hostility = _meters != null ? _meters.Get(MeterType.Hostility) : 50f;
            float hostFactor = hostility / 50f;

            foreach (var zone in new List<char>(_patience.Keys))
            {
                var st = StatusOf(zone);
                if (st.state != ZoneState.Disputed) continue;
                Owner rival = Rival(st);
                int gap = PowerOf(zone, rival) - PowerOf(zone, Owner.Player);
                float gapMult = Mathf.Clamp(1f + powerGapK * gap / Mathf.Max(1f, powerScale), 0.3f, 2.5f);
                AddPatience(zone, -(dailyDecayBase * hostFactor * gapMult));
            }
            EvaluateThresholds();
            ResolveRivalDisputes();
        }

        // ---- Etapa 2: disputas automaticas entre rivales (rojo vs amarillo) ----

        void ResolveRivalDisputes()
        {
            foreach (var zone in new List<char>(Model.Zones))
            {
                var st = StatusOf(zone);
                if (st.state != ZoneState.Disputed) continue;
                if (st.ownerA == Owner.Player || st.ownerB == Owner.Player) continue; // solo rival-vs-rival
                if (NextFloat() > rivalClashChance) continue;
                ClashRivals(zone, st);
            }
        }

        /// <summary>
        /// Pelea AI en una zona disputada por dos rivales: el ganador es ALEATORIO segun el poder
        /// (el fuerte gana mas seguido, pero el debil puede ganar), pierde de a poco y sin avisar.
        /// </summary>
        void ClashRivals(char zone, ZoneStatus st)
        {
            int pa = PowerOf(zone, st.ownerA), pb = PowerOf(zone, st.ownerB);
            float total = pa + pb;
            float pAwin = total > 0f ? pa / total : 0.5f;   // probabilidad de A proporcional a su poder
            Owner winner = NextFloat() < pAwin ? st.ownerA : st.ownerB;
            Owner loser = winner == st.ownerA ? st.ownerB : st.ownerA;
            ApplyDisputeOutcome(zone, winner, loser, winner, 0f, notify: false, forcedLoss: rivalClashStallLoss);
        }

        /// <summary>Tira el ganador probabilistico de una pelea rival (sin aplicar el resultado). Para testeo.</summary>
        public Owner DebugRivalWinnerRoll(char zone)
        {
            var st = StatusOf(zone);
            int pa = PowerOf(zone, st.ownerA), pb = PowerOf(zone, st.ownerB);
            float total = pa + pb;
            float pAwin = total > 0f ? pa / total : 0.5f;
            return NextFloat() < pAwin ? st.ownerA : st.ownerB;
        }

        /// <summary>Fuerza una pelea rival-vs-rival en la zona (para testeo).</summary>
        public void DebugRivalClash(char zone)
        {
            var st = StatusOf(zone);
            if (st.state == ZoneState.Disputed && st.ownerA != Owner.Player && st.ownerB != Owner.Player)
                ClashRivals(zone, st);
            else
                Debug.Log($"[Territory] Zona '{zone}' no es disputa rival-vs-rival.");
        }

        /// <summary>Robo de venta a 'victim' en 'zone': baja la paciencia de esa disputa.</summary>
        public void RegisterSteal(char zone, Owner stealer, Owner victim)
        {
            if (stealer != Owner.Player || !_patience.ContainsKey(zone)) return;
            var st = StatusOf(zone);
            if (st.state != ZoneState.Disputed || (st.ownerA != victim && st.ownerB != victim)) return;
            AddPatience(zone, -stealDecay);
            EvaluateThresholds();
        }

        /// <summary>Ajusta la paciencia de una zona disputada (eventos/negociacion). Solo si ya hay entrada.</summary>
        public void AddPatience(char zone, float delta)
        {
            if (!_patience.ContainsKey(zone)) return;
            _patience[zone] = Mathf.Clamp(_patience[zone] + delta, 0f, patienceMax);
        }

        public float PatienceOf(char zone) => _patience.TryGetValue(zone, out var v) ? v : -1f;
        public float PatienceNormalized(char zone) => _patience.TryGetValue(zone, out var v) ? Mathf.Clamp01(v / patienceMax) : 1f;

        void EvaluateThresholds()
        {
            foreach (var zone in new List<char>(_patience.Keys))
            {
                float p = _patience[zone];
                if (p <= warnThreshold && !_warned.Contains(zone)) { _warned.Add(zone); EmitWarning(zone); }
                if (p <= attackThreshold && !_pendingAttacks.Contains(zone)) _pendingAttacks.Add(zone);
            }
            if (_pendingAttacks.Count > 0 && !_draining && isActiveAndEnabled) StartCoroutine(DrainAttacks());
        }

        IEnumerator DrainAttacks()
        {
            _draining = true;
            while (_pendingAttacks.Count > 0)
            {
                while ((_events != null && _events.IsEventPending) || (_minigame != null && _minigame.IsShowing))
                    yield return null;

                char zone = _pendingAttacks[0];
                _pendingAttacks.RemoveAt(0);
                var st = StatusOf(zone);
                if (!IsDisputant(st, Owner.Player)) continue; // ya no aplica
                StartDispute(zone, Rival(st), Owner.Player); // el enemigo ataca; vos defendes

                while (_minigame != null && _minigame.IsShowing) yield return null;
            }
            _draining = false;
        }

        void EmitWarning(char zone)
        {
            string msg = $"Los rivales de la Zona {zone} estan muy tensos";
            Debug.Log("[Territory] AVISO: " + msg);
            if (Model != null) FloatingText.Spawn(Model.ZoneCentroidWorld(zone), msg, warningColor);
            ZoneTense?.Invoke(zone, msg);
            if (_warningPopup != null) _warningPopup.Show(msg);
        }

        /// <summary>Frase cualitativa de tension (nunca el numero).</summary>
        public string TensionLine(char zone)
        {
            float n = PatienceNormalized(zone);
            if (n > 0.6f) return "tranquilos";
            if (n > 0.3f) return "inquietos";
            return "muy tensos";
        }

        // ---- Acciones del jugador (desde el DisputePopup) ----

        public void PlayerAttack(char zone)
        {
            var st = StatusOf(zone);
            if (!IsDisputant(st, Owner.Player)) return;
            if (_meters != null)
            {
                _meters.Add(MeterType.Reputation, -attackReputationCost); // vos iniciaste el exterminio
                _meters.Add(MeterType.Hostility, attackHostilityGain);
            }
            StartDispute(zone, Owner.Player, Rival(st)); // vos atacas
        }

        public int NegotiationCost(char zone) => Mathf.RoundToInt(negotiateBaseCost * (2f - PatienceNormalized(zone)));

        public bool PlayerNegotiate(char zone)
        {
            if (!_patience.ContainsKey(zone)) return false;
            int cost = NegotiationCost(zone);
            if (_waves == null || !_waves.CanAfford(cost)) return false;
            _waves.Spend(cost);
            _patience[zone] = patienceMax; // tregua: se rellena la paciencia
            _warned.Remove(zone);
            _pendingAttacks.Remove(zone);
            if (Model != null) FloatingText.Spawn(Model.ZoneCentroidWorld(zone), $"Tregua Zona {zone} (-${cost})", new Color(0.5f, 0.8f, 1f));
            return true;
        }

        // ---- Resolucion de disputa ----

        void StartDispute(char zone, Owner attacker, Owner defender)
        {
            int ap = PowerOf(zone, attacker), dp = PowerOf(zone, defender);
            if (_minigame != null)
                _minigame.Play(zone, attacker, defender, ap, dp,
                    (winner, margin) => ApplyDisputeOutcome(zone, attacker, defender, winner, margin));
            else
            {
                Owner winner = ap >= dp ? attacker : defender; // sin UI (tests): por poder
                float margin = Mathf.Clamp01(0.5f + 0.5f * (ap - dp) / Mathf.Max(1f, powerScale));
                ApplyDisputeOutcome(zone, attacker, defender, winner, margin);
            }
        }

        /// <summary>
        /// Aplica el resultado: el perdedor pierde puestos. Por defecto la cantidad sale del margen;
        /// si forcedLoss>=0 se usa esa cantidad fija (para peleas de a poco). notify=false = sin cartel.
        /// </summary>
        public void ApplyDisputeOutcome(char zone, Owner attacker, Owner defender, Owner winner, float margin01,
            bool notify = true, int forcedLoss = -1)
        {
            if (Model == null) return;
            Owner loser = winner == attacker ? defender : attacker;
            var loserStalls = Model.StallsInZone(zone, loser);

            int lossCount = 0;
            if (loserStalls.Count > 0)
            {
                lossCount = forcedLoss >= 0
                    ? Mathf.Clamp(forcedLoss, 1, loserStalls.Count)
                    : Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, maxStallLoss, Mathf.Clamp01(margin01))), 1, loserStalls.Count);
                Vector2 anchor = WinnerAnchor(zone, winner);
                loserStalls.Sort((a, b) => SqrTo(a, anchor).CompareTo(SqrTo(b, anchor)));
                for (int i = 0; i < lossCount; i++)
                {
                    var removed = Model.RemoveAny(loserStalls[i].OriginCell);
                    if (removed != null && removed.View != null) Destroy(removed.View);
                }
            }

            ReconcilePatienceEntries();
            // si sigue en disputa, se calma la tension tras la pelea
            if (_patience.ContainsKey(zone)) { _patience[zone] = patienceMax; _warned.Remove(zone); _pendingAttacks.Remove(zone); }

            if (notify && Model != null)
            {
                string who = winner == Owner.Player ? "Ganaste" : $"Gano {winner}";
                FloatingText.Spawn(Model.ZoneCentroidWorld(zone), $"Zona {zone}: {who} (-{lossCount} a {loser})", _grid.ColorFor(winner));
            }
        }

        Vector2 WinnerAnchor(char zone, Owner winner)
        {
            var ws = Model.StallsInZone(zone, winner);
            if (ws.Count == 0) return Model.ZoneCentroidWorld(zone);
            Vector2 sum = Vector2.zero;
            foreach (var s in ws) sum += Model.CellToWorldCenter(s.OriginCell);
            return sum / ws.Count;
        }

        float SqrTo(PlacedStall s, Vector2 p) => ((Vector2)Model.CellToWorldCenter(s.OriginCell) - p).sqrMagnitude;

        // ---- Debug (MCP) ----

        public void DebugZoneState(char zone)
        {
            var st = StatusOf(zone);
            Debug.Log($"[Territory] Zona '{zone}': {st.state}  A={st.ownerA}({st.countA}) B={st.ownerB}({st.countB})");
        }

        public void DebugPower(char zone, Owner f) => Debug.Log($"[Territory] Poder de {f} en '{zone}' = {PowerOf(zone, f)}");

        public void DebugPatience(char zone) => Debug.Log($"[Territory] Paciencia '{zone}' = {PatienceOf(zone):0.0} (norm {PatienceNormalized(zone):0.00})");

        public void DebugDay() => OnDayPassed();

        /// <summary>Fuerza una entrada de paciencia y la setea (para testear decaimiento/umbral).</summary>
        public void DebugSetPatience(char zone, float value)
        {
            _patience[zone] = Mathf.Clamp(value, 0f, patienceMax);
        }

        public void DebugResolve(char zone, Owner attacker, Owner defender, Owner winner, float margin01)
            => ApplyDisputeOutcome(zone, attacker, defender, winner, margin01);
    }
}
