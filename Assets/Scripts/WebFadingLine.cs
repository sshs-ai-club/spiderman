using UnityEngine;

namespace SpiderPlayground
{
    public sealed class WebFadingLine : MonoBehaviour
    {
        private LineRenderer line;
        private Color originalColor;
        private float remaining;
        private float duration;

        public void Begin(LineRenderer source, Color color, float seconds)
        {
            line = source;
            originalColor = color;
            duration = seconds;
            remaining = seconds;
        }

        private void Update()
        {
            remaining -= Time.deltaTime;
            if (remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            Color faded = originalColor;
            faded.a *= remaining / duration;
            line.startColor = faded;
            line.endColor = faded;
            line.widthMultiplier = 0.045f * (remaining / duration);
        }
    }
}
