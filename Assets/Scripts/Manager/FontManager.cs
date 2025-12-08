using UnityEngine;
using TMPro;

public class FontManager : MonoBehaviour
{
    [Tooltip("The font to replace all other fonts with.")]
    public TMP_FontAsset targetFont;

    [ContextMenu("Replace All Fonts")]
    public void ReplaceAllFonts()
    {
        if (targetFont == null)
        {
            Debug.LogError("FontManager: Target Font is not assigned. Please assign a font to the 'Target Font' field.");
            return;
        }

        // Find all TMP_Text components in the scene, including inactive ones.
        // Resources.FindObjectsOfTypeAll returns assets (prefabs) as well, so we need to filter them.
        TMP_Text[] allTexts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        int count = 0;

        foreach (TMP_Text text in allTexts)
        {
            // Check if the object is part of a scene (not a project asset/prefab)
            // gameObject.scene.rootCount > 0 ensures it's in a loaded scene.
            if (text.gameObject.scene.rootCount > 0)
            {
                if (text.font != targetFont)
                {
                    text.font = targetFont;
                    
                    // Mark the object as dirty in editor so the change is saved
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEditor.EditorUtility.SetDirty(text);
                    }
#endif
                    count++;
                }
            }
        }

        Debug.Log($"FontManager: Successfully replaced font on {count} TextMeshPro objects.");
    }
}
