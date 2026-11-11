using UnityEngine;

namespace Ibit.Plataform
{
    /// <summary>
    /// Restores the serialized sprite-frame animation component used by the
    /// existing target/relax prefabs. This is a minimal replacement for a lost
    /// script reference; no prefab or device connection changes are required.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFrameAnimator : MonoBehaviour
    {
        // Names must remain "sprites" and "interval": these fields are
        // already serialized by the existing prefabs.
        [SerializeField] private Sprite[] sprites;
        [SerializeField] private float interval = 0.1f;

        private SpriteRenderer spriteRenderer;
        private int frameIndex;
        private float elapsed;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            frameIndex = 0;
            elapsed = 0f;
            if (spriteRenderer != null && sprites != null &&
                sprites.Length > 0 && sprites[0] != null)
                spriteRenderer.sprite = sprites[0];
        }

        private void Update()
        {
            if (spriteRenderer == null || sprites == null || sprites.Length < 2)
                return;

            float frameDuration = Mathf.Max(0.01f, interval);
            elapsed += Time.deltaTime;
            if (elapsed < frameDuration)
                return;

            int steps = Mathf.FloorToInt(elapsed / frameDuration);
            elapsed -= steps * frameDuration;
            frameIndex = (frameIndex + steps) % sprites.Length;

            if (sprites[frameIndex] != null)
                spriteRenderer.sprite = sprites[frameIndex];
        }
    }
}
