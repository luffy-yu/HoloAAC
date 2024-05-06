using Microsoft.MixedReality.Toolkit.UI;
using UnityEngine;

public class DialogController : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Assign DialogMediume_192x128.prefab")]
    private GameObject dialogPrefabMedium;

    /// <summary>
    /// Medium Dialog example prefab to display
    /// </summary>
    public GameObject DialogPrefabMedium
    {
        get => dialogPrefabMedium;
        set => dialogPrefabMedium = value;
    }

    /// <summary>
    /// Opens confirmation dialog example
    /// </summary>
    public void OpenConfirmationDialogMedium(string title, string content)
    {
        Dialog.Open(DialogPrefabMedium, DialogButtonType.OK, title, content, true);
    }

    /// <summary>
    /// Opens choice dialog example
    /// </summary>
    public void OpenChoiceDialogMedium()
    {
        Dialog myDialog = Dialog.Open(DialogPrefabMedium, DialogButtonType.Yes | DialogButtonType.No, "Choice Dialog, Medium, Far", "This is an example of a medium dialog with a choice message for the user, placed at far interaction range", false);
        if (myDialog != null)
        {
            myDialog.OnClosed += OnClosedDialogEvent;
        }
    }

    private void OnClosedDialogEvent(DialogResult obj)
    {
        if (obj.Result == DialogButtonType.Yes)
        {
            Debug.Log(obj.Result);
        }
    }
}