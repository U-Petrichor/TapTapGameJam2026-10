using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TapTapGameJam.UI
{
    public class InteractionObjectUI : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text interactionPrompt;
        Color defaultColor = new Color(53f / 255f, 53f / 255f, 53f / 255f, 250f / 255f);

        public void RefreshInteractionPrompt(string prompt)
        {
            interactionPrompt.text = prompt;
        }
        public void OnSelected()
        {
            background.color = Color.yellow;
        }
        public void OnDeselected()
        {
            background.color = defaultColor;
        }
    }

}