using UnityEngine;

namespace TycoWorld.Gameplay
{
    /// <summary>
    /// A shop front built from primitives at a baked ShopSite (spec #6 §2): counter on the property line, awning over the
    /// gutter, a name board, a slab across the gutter, a keeper behind the counter while open, and a lamp for the
    /// 24-hour minimarket. Root at the counter's ground point, facing the road (+Z).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopStall : MonoBehaviour
    {
        public const float CounterHeight = 0.95f;

        ShopSite site;
        Transform keeper;
        Light lamp;
        Renderer[] renderers;
        bool visible = true, open = true;

        public ShopSite Site => site;
        public ShopCounterInteractable Counter { get; private set; }
        public bool KeeperVisible => keeper != null && keeper.gameObject.activeSelf;
        /// <summary>The keeper's animated body (null with the primitive figure).</summary>
        public CharacterRig KeeperRig { get; private set; }

        static Color32 Awning(ShopType type)
        {
            switch (type)
            {
                case ShopType.Warung: return new Color32(40, 110, 170, 255);
                case ShopType.WarungMakan: return new Color32(215, 120, 40, 255);
                case ShopType.Distro: return new Color32(35, 35, 38, 255);
                case ShopType.Konter: return new Color32(190, 40, 45, 255);
                case ShopType.Apotek: return new Color32(40, 150, 90, 255);
                case ShopType.Bengkel: return new Color32(120, 120, 125, 255);
                default: return new Color32(30, 80, 190, 255);
            }
        }

        public static ShopStall Build(Transform parent, ShopSite site, Palette palette, Font font, KampungDirector director)
        {
            var go = new GameObject("Shop_" + site.id);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(site.counter, Quaternion.Euler(0f, site.yaw, 0f));
            var stall = go.AddComponent<ShopStall>();
            stall.site = site;
            var t = go.transform;
            var wood = palette.Get(new Color32(120, 82, 50, 255));
            var concrete = palette.Get(new Color32(170, 168, 160, 255));
            var awning = palette.Get(Awning(site.type));
            var white = palette.Get(new Color32(235, 235, 230, 255));

            // Counter (the E target) straddling the property line.
            var counter = Primitives.Part(PrimitiveType.Cube, t, "Counter", new Vector3(0f, CounterHeight * 0.5f, 0.05f), new Vector3(1.7f, CounterHeight, 0.55f), wood, keepCollider: true);
            var interactable = counter.AddComponent<ShopCounterInteractable>();
            interactable.Configure(director, stall);
            interactable.SetInteractionId("shop:" + site.id);
            stall.Counter = interactable;
            // Goods on the counter.
            var goods = new SeededRandom(SeededRandom.Hash(site.id));
            for (int i = 0; i < 4; i++)
            {
                var c = new Color32((byte)goods.Range(60, 250), (byte)goods.Range(60, 250), (byte)goods.Range(60, 250), 255);
                float x = -0.6f + i * 0.4f;
                Primitives.Part(PrimitiveType.Cube, t, "Goods" + i, new Vector3(x, CounterHeight + 0.09f, 0.05f), new Vector3(0.22f, 0.18f, 0.2f), palette.Get(c));
            }
            // Slab across the gutter, as at the gates.
            if (site.slabLength > 0.05f)
            {
                float length = site.slabLength + 0.25f;
                Primitives.Part(PrimitiveType.Cube, t, "Slab", new Vector3(0f, 0.0825f, 0.32f + length * 0.5f), new Vector3(2.2f, 0.165f, length), concrete, keepCollider: true);
            }
            // Awning on two front posts.
            float depth = Mathf.Max(1.2f, site.slabLength + 0.8f);
            var roof = Primitives.Part(PrimitiveType.Cube, t, "Awning", new Vector3(0f, 2.45f, depth * 0.5f), new Vector3(2.6f, 0.05f, depth + 0.3f), awning);
            roof.transform.localRotation = Quaternion.Euler(-9f, 0f, 0f);
            Primitives.Part(PrimitiveType.Cube, t, "PostL", new Vector3(-1.22f, 1.15f, depth), new Vector3(0.06f, 2.3f, 0.06f), white);
            Primitives.Part(PrimitiveType.Cube, t, "PostR", new Vector3(1.22f, 1.15f, depth), new Vector3(0.06f, 2.3f, 0.06f), white);
            // Name board above the awning, readable from the road.
            Primitives.Part(PrimitiveType.Cube, t, "Board", new Vector3(0f, 2.95f, 0.1f), new Vector3(2.2f, 0.5f, 0.06f), awning);
            if (font != null)
            {
                var label = new GameObject("Name");
                label.transform.SetParent(t, false);
                label.transform.localPosition = new Vector3(0f, 2.95f, 0.14f);
                label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var text = label.AddComponent<TextMesh>();
                text.text = site.name;
                text.font = font;
                text.fontSize = 48;
                text.characterSize = 0.035f * Mathf.Min(1f, 22f / Mathf.Max(1, site.name.Length));
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = Color.white;
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            // The keeper.
            ulong keeperSeed = SeededRandom.Hash(site.id + ":keeper");
            var rig = CharacterFactory.Spawn(director != null ? director.Characters : null, t, "Keeper", keeperSeed, palette, woman: (keeperSeed & 1UL) == 0UL);
            stall.KeeperRig = rig;
            stall.keeper = rig != null ? rig.Root : PersonFigure.Build(t, "Keeper", PersonLook.From(keeperSeed), palette).Root;
            // Beside the counter, turned toward the customer: flush fronts leave no room behind it.
            stall.keeper.localPosition = new Vector3(1.2f, 0f, 0.15f);
            stall.keeper.localRotation = Quaternion.Euler(0f, -25f, 0f);
            if (site.type == ShopType.Minimarket)
            {
                var lampGo = new GameObject("Lamp");
                lampGo.transform.SetParent(t, false);
                lampGo.transform.localPosition = new Vector3(0f, 2.25f, 0.6f);
                stall.lamp = lampGo.AddComponent<Light>();
                stall.lamp.type = LightType.Point;
                stall.lamp.range = 7f;
                stall.lamp.intensity = 2.2f;
                stall.lamp.color = new Color(0.85f, 0.95f, 1f);
                stall.lamp.shadows = LightShadows.None;
            }
            stall.renderers = go.GetComponentsInChildren<Renderer>(true);
            return stall;
        }

        /// <summary>Keeper present and (for the minimarket) the lamp on at night; closed shops show an empty counter.</summary>
        public void SetOpen(bool isOpen, bool dark)
        {
            open = isOpen;
            if (keeper != null) keeper.gameObject.SetActive(isOpen && visible);
            if (lamp != null) lamp.enabled = isOpen && dark && visible;
        }

        /// <summary>Distance culling (runtime objects are outside the generated chunks the DistanceCuller handles).</summary>
        public void SetVisible(bool show)
        {
            if (show == visible) return;
            visible = show;
            foreach (var r in renderers)
                if (r != null) r.enabled = show;
            if (keeper != null) keeper.gameObject.SetActive(open && show);
            if (lamp != null && !show) lamp.enabled = false;
        }
    }
}
