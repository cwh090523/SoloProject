using UnityEngine;
using UnityEngine.UIElements;

namespace UI
{
    public static class PanelOpenEffect
    {
        public static void Play(VisualElement panel, float duration = 0.22f, float startScale = 0.96f)
        {
            if (panel == null)
                return;

            float safeDuration = Mathf.Max(0.01f, duration);
            float safeStartScale = Mathf.Clamp(startScale, 0.8f, 1f);
            float startTime = Time.unscaledTime;

            panel.style.opacity = 0f;
            panel.style.scale = new Scale(new Vector3(safeStartScale, safeStartScale, 1f));

            IVisualElementScheduledItem scheduledItem = null;
            scheduledItem = panel.schedule.Execute(() =>
            {
                float elapsed = Time.unscaledTime - startTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float scale = Mathf.Lerp(safeStartScale, 1f, eased);

                panel.style.opacity = eased;
                panel.style.scale = new Scale(new Vector3(scale, scale, 1f));

                if (t < 1f)
                    return;

                panel.style.opacity = 1f;
                panel.style.scale = new Scale(Vector3.one);
                scheduledItem?.Pause();
            }).Every(16);
        }
    }
}
