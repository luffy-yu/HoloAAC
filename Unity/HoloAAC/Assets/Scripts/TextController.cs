using TMPro;
using UnityEngine;

public class TextController : MonoBehaviour
{
    [Tooltip("Deselected color for keywords")]
    [SerializeField] private Color deselectedColor = Color.white;
    [Tooltip("Selected color for keywords")]
    [SerializeField] private Color selectedColor = Color.red;

    // change text color
    public TMP_Text ChangeColor(TMP_Text text)
    {
        if (text == null) return text;

        if (IsSelected(text))
        {
            //set deselected color
            text.color = deselectedColor;
        } else
        {
            text.color = selectedColor;
        }
        return text;
    }

    public void Select(TMP_Text text)
    {
        if (text == null) return;

        text.color = selectedColor;
    }

    // check whether selected by text color
    public bool IsSelected(TMP_Text text)
    {
        return text.color == selectedColor;
    }

    public Color GetDeselectedColor()
    {
        return deselectedColor;
    }

    public Color GetSelectedColor()
    {
        return selectedColor;
    }

    // set select/deselect
    public void SetColor(TMP_Text text, bool selected)
    {
        if (selected)
        {
            //set deselected color
            text.color = selectedColor;
        }
        else
        {
            text.color = deselectedColor;
        }
    }
}