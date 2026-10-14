using System;
using System.Collections.Generic;
using System.Globalization;
using TycoWorld.DayCycle;
using TycoWorld.Player;
using TycoWorld.WorldGen;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// Runs kampung life (spec #6 §4): waits for the world and the player, loads the save, builds the shops and the kos
    /// bed, then each frame drains needs with the game clock, faints the exhausted, culls its own objects and drives the
    /// gameplay HUD, the shop window and the phone. One per scene, on the "Gameplay" root of World.unity.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class KampungDirector : MonoBehaviour
    {
        public const string MissingDataWarning = "[Gameplay] KampungData kosong. Jalankan Tyco World > Gameplay > Bake Kampung Data.";
        public const string KosDoorId = "kos-door";
        public const float ShopCullDistance = 160f;
        public const float FadeSeconds = 0.9f;
        public const float WakeHours = 6.5f;
        public const string HungryToast = "Perut keroncongan. Cari makan di warung.";
        public const string SleepyToast = "Ngantuk berat. Pulang dan tidur di kos.";
        public const string NotSleepyToast = "Belum ngantuk.";

        [SerializeField] KampungData data;
        [SerializeField] ItemCatalog catalog;
        [SerializeField] Material baseMaterial;
        [Tooltip("Parked warga bikes are copies of this (the Motorbike prefab); empty builds plain code bikes.")]
        [SerializeField] GameObject bikePrefab;

        public Func<FirstPersonPlayer> FindPlayer = () => FindAnyObjectByType<FirstPersonPlayer>();
        public Func<DayCycleDirector> FindDayCycle = () => FindAnyObjectByType<DayCycleDirector>();
        public Func<bool> WorldReady = () => FindAnyObjectByType<GeneratedRoot>() != null;
        /// <summary>Where the game is saved; tests swap in a MemorySaveBackend before Initialize.</summary>
        public ISaveBackend SaveBackend { get; set; } = new PlayerPrefsSaveBackend();
        /// <summary>Keyboard shortcuts (Tab phone, E closes the shop); off in tests.</summary>
        public bool ReadKeyboard { get; set; } = true;
        /// <summary>Creates the default systems (pedestrians, vehicles, missions) when the world is ready; tests add their own.</summary>
        public bool CreateDefaultSystems { get; set; } = true;

        readonly List<ShopStall> stalls = new List<ShopStall>();
        readonly List<IKampungSystem> systems = new List<IKampungSystem>();
        Palette palette;
        Transform objectsRoot;
        DayCycleDirector dayCycle;
        float lastHours = -1f, manualHours = WakeHours, cullTimer, fadeTimer;
        Action fadeAction;
        bool fadeActionDone, warnedHungry, warnedSleepy, holdsSprintLock, holdsModalLock;
        readonly object modalOwner = new object();

        public KampungData Data => data;
        public ItemCatalog Catalog => catalog;
        public GameObject BikePrefab => bikePrefab;
        public Palette Palette => palette;
        public GameState State { get; } = new GameState();
        public FirstPersonPlayer Player { get; private set; }
        public DayCycleDirector DayCycle => dayCycle;
        public bool IsInitialized { get; private set; }
        public bool IsReady { get; private set; }
        public GameplayHudView Hud { get; private set; }
        public ShopView Shop { get; private set; }
        public PhoneView Phone { get; private set; }
        public IReadOnlyList<ShopStall> Stalls => stalls;
        public bool IsModalOpen => (Shop != null && Shop.IsOpen) || (Phone != null && Phone.IsOpen);
        public bool IsFading => fadeTimer > 0f;
        /// <summary>Game hours 0..24 (the day cycle's clock, or a fallback clock when there is none).</summary>
        public float Hours => dayCycle != null && dayCycle.IsInitialized ? dayCycle.Clock.Hours : manualHours;
        public bool IsNight => dayCycle != null && dayCycle.IsInitialized ? dayCycle.IsHorror : Hours >= 19f || Hours < 5f;
        public bool IsDark => Hours >= 18f || Hours < 6f;
        public string ClockText => Mathf.FloorToInt(Hours).ToString("00", CultureInfo.InvariantCulture) + ":" +
            Mathf.FloorToInt((Hours - Mathf.Floor(Hours)) * 60f).ToString("00", CultureInfo.InvariantCulture);
        /// <summary>The "Gameplay/Objects" container for runtime objects (never under the generated root).</summary>
        public Transform ObjectsRoot => objectsRoot;
        /// <summary>Height of the flat world the baked lanes lie on (0 in the generated world; test fixtures move it).</summary>
        public float GroundLevel { get; set; }

        /// <summary>The walking lanes as a graph, built on first use and shared by pedestrians and missions.</summary>
        public LaneGraph Lanes
        {
            get
            {
                if (lanes == null && data != null) lanes = new LaneGraph(data.walkLanes, data.walkNodes);
                return lanes;
            }
        }

        LaneGraph lanes;

        public event Action Ready;

        public void Configure(KampungData kampung, ItemCatalog items, Material template, GameObject bike = null)
        {
            data = kampung;
            catalog = items;
            baseMaterial = template;
            bikePrefab = bike;
        }

        public void AddSystem(IKampungSystem system)
        {
            if (system == null || systems.Contains(system)) return;
            systems.Add(system);
            systems.Sort((a, b) => a.Order.CompareTo(b.Order));
            if (IsReady) system.Begin(this);
        }

        public T GetSystem<T>() where T : class, IKampungSystem
        {
            foreach (var s in systems)
                if (s is T t) return t;
            return null;
        }

        void Awake() => Initialize();

        void Update()
        {
            if (Application.isPlaying) Tick(Time.deltaTime);
        }

        void OnApplicationQuit() => SaveNow();

        void OnDisable()
        {
            if (IsReady) SaveNow();
        }

        void OnDestroy()
        {
            ReleaseLocks();
            foreach (var s in systems) s.End();
            if (palette != null) palette.Dispose();
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            if (catalog == null) catalog = ItemCatalog.CreateDefault();
            palette = new Palette(baseMaterial);
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (root != null)
            {
                Hud = new GameplayHudView(root);
                Shop = new ShopView(root);
                Phone = new PhoneView(root);
                Shop.BuyRequested += Buy;
                Shop.CloseRequested += CloseShop;
                Phone.UseRequested += Use;
                Phone.WearRequested += Wear;
                Phone.CloseRequested += ClosePhone;
                Phone.Select(PhoneTab.Dompet);
                Hud.Visible = false;
            }
            var container = transform.Find("Objects");
            if (container == null)
            {
                container = new GameObject("Objects").transform;
                container.SetParent(transform, false);
            }
            objectsRoot = container;
            IsInitialized = true;
        }

        public void Tick(float dt)
        {
            if (!IsInitialized) Initialize();
            if (!IsReady)
            {
                TryBegin();
                return;
            }
            AdvanceClock(dt);
            TickNeeds();
            TickFade(dt);
            cullTimer -= dt;
            if (cullTimer <= 0f)
            {
                cullTimer = 0.5f;
                UpdateShops();
            }
            if (ReadKeyboard) ReadKeys();
            foreach (var s in systems) s.Tick(dt);
            if (Hud != null) Hud.Render(State, dt);
            if (Phone != null && Phone.IsOpen) RenderPhone();
        }

        void TryBegin()
        {
            var player = FindPlayer != null ? FindPlayer() : null;
            if (player == null || !player.IsInitialized || WorldReady == null || !WorldReady()) return;
            Player = player;
            dayCycle = FindDayCycle != null ? FindDayCycle() : null;
            if (data == null)
            {
                Debug.LogWarning(MissingDataWarning);
                data = ScriptableObject.CreateInstance<KampungData>();
                data.hideFlags = HideFlags.DontSave;
            }
            var store = new SaveStore(SaveBackend);
            bool continuing = store.HasSave;
            var saved = store.Load();
            State.Load(saved);
            if (dayCycle != null && dayCycle.IsInitialized) dayCycle.Clock.SetTime(saved.hours);
            else manualHours = saved.hours;
            lastHours = Hours;
            BuildShops();
            ReplaceKosDoor();
            if (CreateDefaultSystems) DefaultSystems.AddTo(this);
            IsReady = true;
            if (Hud != null)
            {
                Hud.Visible = true;
                Hud.Render(State, 0f);
            }
            foreach (var s in systems) s.Begin(this);
            Toast(continuing
                ? "Hari " + State.Day + ". Uangmu " + RupiahFormat.Format(State.Wallet.Balance) + "."
                : "Selamat datang di Sukabirus. Uangmu " + RupiahFormat.Format(State.Wallet.Balance) + ". Tab untuk buka HP.");
            Ready?.Invoke();
        }

        // ---------- clock and needs ----------

        void AdvanceClock(float dt)
        {
            if (dayCycle == null || !dayCycle.IsInitialized) manualHours = Mathf.Repeat(manualHours + dt / 60f, 24f);
            float now = Hours;
            float delta = now - lastHours;
            if (delta < -12f)
            {
                delta += 24f;
                State.NextDay();
                Toast("Lewat tengah malam. Hari ke-" + State.Day + ".");
            }
            if (delta > 0f && delta < 2f)
            {
                bool sprinting = Player != null && Player.IsSprinting;
                State.Needs.Tick(delta, IsNight, State.IsWarm, sprinting);
            }
            lastHours = now;
        }

        /// <summary>Sets the clock (sleep, fainting) without draining needs for the skipped hours; past midnight starts a new day.</summary>
        public void JumpClock(float targetHours)
        {
            targetHours = Mathf.Repeat(targetHours, 24f);
            if (targetHours <= Hours) State.NextDay();
            if (dayCycle != null && dayCycle.IsInitialized) dayCycle.Clock.SetTime(targetHours);
            else manualHours = targetHours;
            lastHours = targetHours;
        }

        void TickNeeds()
        {
            var needs = State.Needs;
            if (needs.Lapar < Needs.LowWarning && !warnedHungry)
            {
                warnedHungry = true;
                Toast(HungryToast);
            }
            else if (needs.Lapar > Needs.LowWarning + 10f) warnedHungry = false;
            if (needs.Energi < Needs.LowWarning && !warnedSleepy)
            {
                warnedSleepy = true;
                Toast(SleepyToast);
            }
            else if (needs.Energi > Needs.LowWarning + 10f) warnedSleepy = false;
            if (Player != null)
            {
                if (needs.IsStarving && !holdsSprintLock)
                {
                    Player.Locks.Acquire(this, PlayerAbility.Sprint);
                    holdsSprintLock = true;
                }
                else if (!needs.IsStarving && holdsSprintLock)
                {
                    Player.Locks.Release(this);
                    holdsSprintLock = false;
                }
            }
            if (needs.IsExhausted && !IsFading) Faint();
        }

        // ---------- sleep and fainting ----------

        public void Sleep()
        {
            if (IsFading) return;
            float h = Hours;
            bool night = h >= 19f || h < 5f;
            if (!night && State.Needs.Energi > 80f)
            {
                Toast(NotSleepyToast);
                return;
            }
            StartFade(() =>
            {
                if (night)
                {
                    JumpClock(WakeHours);
                    State.Needs.Set(State.Needs.Lapar - 20f, Needs.Max);
                }
                else
                {
                    JumpClock(h + 3f);
                    State.Needs.Restore(-10f, 35f);
                }
                long allowance = State.WakeUp();
                SaveNow();
                Toast(night ? "Pagi, hari ke-" + State.Day + "." : "Tidur siang sebentar.");
                if (allowance > 0) Toast("Uang jajan dari orang tua masuk: " + RupiahFormat.Format(allowance) + ".");
            });
        }

        public void Faint()
        {
            StartFade(() =>
            {
                CloseShop();
                ClosePhone();
                SendHome();
                float h = Hours;
                JumpClock(h >= 19f || h < 5f ? 7f : h + 4f);
                long taken = State.Faint();
                SaveNow();
                Toast("Kamu pingsan kelelahan. Warga mengantarmu ke kos" + (taken > 0 ? " (ongkos " + RupiahFormat.Format(taken) + ")." : "."));
            });
        }

        /// <summary>Puts the player on the kos spawn point (off any vehicle).</summary>
        public void SendHome()
        {
            if (Player == null) return;
            var spawner = FindAnyObjectByType<PlayerSpawner>();
            var point = spawner != null ? spawner.SpawnPoint : null;
            if (point == null) return;
            var feet = point.Feet;
            if (spawner.TryFindSpawn(point.Feet, out var safe)) feet = safe;
            Player.Teleport(feet, point.Yaw);
        }

        /// <summary>Fades to black, runs <paramref name="atBlack"/> unseen (caught by warga...), and fades back.</summary>
        public void FadeThrough(Action atBlack)
        {
            if (IsFading) return;
            StartFade(() =>
            {
                CloseShop();
                ClosePhone();
                atBlack?.Invoke();
            });
        }

        void StartFade(Action atBlack)
        {
            fadeTimer = FadeSeconds * 2f;
            fadeAction = atBlack;
            fadeActionDone = false;
        }

        void TickFade(float dt)
        {
            if (fadeTimer <= 0f) return;
            fadeTimer -= dt;
            float t = 1f - Mathf.Clamp01(fadeTimer / (FadeSeconds * 2f));
            float alpha = t < 0.5f ? t * 2f : (1f - t) * 2f;
            if (!fadeActionDone && t >= 0.5f)
            {
                fadeActionDone = true;
                fadeAction?.Invoke();
                fadeAction = null;
            }
            if (fadeTimer <= 0f) alpha = 0f;
            if (Hud != null) Hud.SetFade(alpha);
        }

        /// <summary>Completes a running fade at once (tests, MCP).</summary>
        public void FinishFade()
        {
            if (fadeTimer <= 0f) return;
            if (!fadeActionDone)
            {
                fadeActionDone = true;
                fadeAction?.Invoke();
                fadeAction = null;
            }
            fadeTimer = 0f;
            if (Hud != null) Hud.SetFade(0f);
        }

        // ---------- shops ----------

        public bool IsShopOpen(ShopSite site) => site != null && OpeningHours.IsOpen(site.type, Hours);

        void BuildShops()
        {
            var shopsRoot = new GameObject("Shops").transform;
            shopsRoot.SetParent(objectsRoot, false);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var site in data.shops)
                if (site != null) stalls.Add(ShopStall.Build(shopsRoot, site, palette, font, this));
            Physics.SyncTransforms();
            UpdateShops();
        }

        void UpdateShops()
        {
            var viewer = Player != null ? Player.transform.position : Vector3.zero;
            bool dark = IsDark;
            foreach (var stall in stalls)
            {
                if (stall == null) continue;
                var d = stall.transform.position - viewer;
                d.y = 0f;
                stall.SetVisible(d.sqrMagnitude < ShopCullDistance * ShopCullDistance);
                stall.SetOpen(IsShopOpen(stall.Site), dark);
            }
        }

        public void OpenShop(ShopStall stall)
        {
            if (stall == null || Shop == null || IsModalOpen) return;
            var items = new List<ItemDefinition>();
            foreach (var id in ItemCatalog.Stock(stall.Site.type))
            {
                var item = catalog.Find(id);
                if (item != null) items.Add(item);
            }
            Shop.Open(stall.Site, items, State);
            AcquireModal();
        }

        public void CloseShop()
        {
            if (Shop == null || !Shop.IsOpen) return;
            Shop.Close();
            ReleaseModalIfIdle();
        }

        void Buy(ItemDefinition item)
        {
            var result = State.Buy(item);
            switch (result)
            {
                case PurchaseResult.Bought: Shop.SetMessage(item.displayName + " masuk tas. Buka HP (Tab) > Tas untuk makan/minum.", true); break;
                case PurchaseResult.Eaten: Shop.SetMessage("Kamu menghabiskan " + item.displayName.ToLowerInvariant() + ". Kenyang.", true); break;
                case PurchaseResult.Worn: Shop.SetMessage(item.displayName + " dibeli dan langsung dipakai.", true); break;
                case PurchaseResult.AlreadyOwned: Shop.SetMessage(item.displayName + " sudah punya. Sekarang dipakai.", true); break;
                case PurchaseResult.DataActive: Shop.SetMessage("Paket data aktif sampai tengah malam. Cek aplikasi Kerjaan di HP.", true); break;
                case PurchaseResult.Serviced: Shop.SetMessage(ServiceMessage(), true); break;
                case PurchaseResult.NotEnoughMoney: Shop.SetMessage("Uangnya kurang.", false); break;
            }
            if (result == PurchaseResult.Serviced) Serviced?.Invoke(item);
            Shop.Refresh(State);
        }

        /// <summary>A bengkel service was paid for (the vehicle system repaints the ridden bike and clears the heat).</summary>
        public event Action<ItemDefinition> Serviced;
        /// <summary>Optional text for a paid service, set by the vehicle system.</summary>
        public Func<string> ServiceText;

        string ServiceMessage() => ServiceText != null ? ServiceText() : "Beres. Motormu sudah dicat ulang.";

        // ---------- phone ----------

        public void TogglePhone()
        {
            if (Phone == null || (Shop != null && Shop.IsOpen) || IsFading) return;
            if (Phone.IsOpen) ClosePhone();
            else
            {
                Phone.Open();
                AcquireModal();
                RenderPhone();
            }
        }

        public void ClosePhone()
        {
            if (Phone == null || !Phone.IsOpen) return;
            Phone.Close();
            ReleaseModalIfIdle();
        }

        /// <summary>Jobs shown in the phone's Kerjaan tab (the mission system fills it).</summary>
        public Func<IReadOnlyList<JobListing>> Jobs;
        /// <summary>Extra map markers, one source per system (the mission target, a stolen bike...).</summary>
        public readonly List<Func<IEnumerable<MapMarker>>> MarkerSources = new List<Func<IEnumerable<MapMarker>>>();

        void RenderPhone()
        {
            var markers = new List<MapMarker>();
            foreach (var site in data.shops)
                markers.Add(new MapMarker { Position = new Vector2(site.counter.x, site.counter.z), Color = new Color(0.3f, 0.7f, 1f), Size = 4f });
            foreach (var spot in data.spots)
                if (spot.kind == SpotKind.Kos) markers.Add(new MapMarker { Position = new Vector2(spot.position.x, spot.position.z), Color = new Color(1f, 0.85f, 0.2f), Size = 6f });
            foreach (var source in MarkerSources)
            {
                var extra = source();
                if (extra != null) markers.AddRange(extra);
            }
            var p = Player != null ? Player.transform.position : Vector3.zero;
            float yaw = Player != null && Player.View != null ? Player.View.eulerAngles.y : 0f;
            Phone.Render(State, catalog, ClockText, Jobs != null ? Jobs() : null, data, new Vector2(p.x, p.z), yaw, markers);
        }

        void Use(ItemDefinition item)
        {
            if (State.Use(item)) Toast("Kamu " + (item.category == ItemCategory.Drink ? "minum " : "makan ") + item.displayName.ToLowerInvariant() + ".");
        }

        void Wear(string id)
        {
            if (!State.Wear(id)) return;
            var item = catalog.Find(id);
            Toast(item != null ? "Ganti baju: " + item.displayName + "." : "Ganti ke kaos lama.");
        }

        // ---------- modal locks, keys, toasts ----------

        void AcquireModal()
        {
            if (Player == null || holdsModalLock) return;
            Player.Locks.Acquire(modalOwner, PlayerAbility.All);
            Player.UnlockCursor();
            holdsModalLock = true;
        }

        void ReleaseModalIfIdle()
        {
            if (IsModalOpen || Player == null || !holdsModalLock) return;
            Player.Locks.Release(modalOwner);
            holdsModalLock = false;
            if (Player.Locks.Allows(PlayerAbility.Look)) Player.LockCursor();
        }

        void ReleaseLocks()
        {
            if (Player == null) return;
            if (holdsModalLock) Player.Locks.Release(modalOwner);
            if (holdsSprintLock) Player.Locks.Release(this);
            holdsModalLock = holdsSprintLock = false;
        }

        void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.tabKey.wasPressedThisFrame) TogglePhone();
            else if (keyboard.eKey.wasPressedThisFrame && Shop != null && Shop.IsOpen) CloseShop();
        }

        public void Toast(string text)
        {
            if (Hud != null) Hud.Toast(text);
        }

        public void SaveNow()
        {
            if (!IsReady) return;
            new SaveStore(SaveBackend).Save(State.ToSave(Hours));
        }

        void ReplaceKosDoor()
        {
            foreach (var door in FindObjectsByType<Interactable>(FindObjectsInactive.Exclude))
            {
                if (door == null || door.InteractionId != KosDoorId || door is KosSleepInteractable) continue;
                var go = door.gameObject;
                var source = door.AudioOutput;
                if (Application.isPlaying) Destroy(door);
                else DestroyImmediate(door);
                var bed = go.AddComponent<KosSleepInteractable>();
                bed.Configure(this);
                bed.SetInteractionId(KosDoorId);
                if (source != null) bed.SetAudioSource(source);
            }
        }
    }

    /// <summary>A gameplay system the director ticks after it is ready (pedestrians, vehicles, missions...).</summary>
    public interface IKampungSystem
    {
        int Order { get; }
        void Begin(KampungDirector director);
        void Tick(float dt);
        void End();
    }

}
