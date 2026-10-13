using UnityEngine;
using UnityEngine.UIElements;

namespace TycoWorld.Player
{
    /// <summary>Renders FirstPersonPlayer.Hud into this GameObject's UIDocument (built lazily once the panel exists).</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class PlayerHud : MonoBehaviour, IPlayerModule
    {
        HudView view;

        public int Order => 40;
        public HudView View => view;

        public void Initialize(FirstPersonPlayer player) => TryBuild();

        public void Tick(FirstPersonPlayer player, float dt)
        {
            if (view == null) TryBuild();
            if (view != null) view.Render(player.Hud);
        }

        void TryBuild()
        {
            var document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null) return;
            view = new HudView(document.rootVisualElement);
        }
    }
}
