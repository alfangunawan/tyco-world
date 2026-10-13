using UnityEngine;

namespace TycoWorld.Player
{
    /// <summary>Lives on the camera: finds the Interactable in view, shows its prompt, and uses it on Interact.</summary>
    public sealed class Interactor : MonoBehaviour, IPlayerModule
    {
        public int Order => 20;
        public Interactable Focus { get; private set; }

        public void Initialize(FirstPersonPlayer player) { }

        public void Tick(FirstPersonPlayer player, float dt)
        {
            var t = player.Tuning.interaction;
            Focus = player.Locks.Allows(PlayerAbility.Interact)
                ? FindFocus(new Ray(transform.position, transform.forward), t.range, t.sphereRadius)
                : null;
            var context = new InteractionContext(player, player.Hud, transform.position);
            bool usable = Focus != null && Focus.CanInteract(context);
            player.Hud.SetPrompt(usable ? Focus.GetPrompt(context) : "");
            if (usable && player.Intent.Interact) Focus.Interact(context);
        }

        /// <summary>Ray first; if it hits nothing interactable, a thin sphere cast no farther than the ray's hit.</summary>
        public static Interactable FindFocus(Ray ray, float range, float sphereRadius)
        {
            float reach = range;
            if (Physics.Raycast(ray, out var hit, range, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
            {
                var direct = hit.collider.GetComponentInParent<Interactable>();
                if (direct != null) return direct;
                reach = hit.distance;
            }
            if (Physics.SphereCast(ray, sphereRadius, out var sphereHit, reach, PlayerLayers.QueryMask, QueryTriggerInteraction.Ignore))
                return sphereHit.collider.GetComponentInParent<Interactable>();
            return null;
        }
    }
}
