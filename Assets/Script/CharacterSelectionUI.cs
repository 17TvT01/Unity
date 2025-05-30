using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionUI : MonoBehaviour
{
    public Button nextButton;
    public Button previousButton;
    public Button selectButton;

    private void Start()
    {
        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextButtonClick);
            
        if (previousButton != null)
            previousButton.onClick.AddListener(OnPreviousButtonClick);
            
        if (selectButton != null)
            selectButton.onClick.AddListener(OnSelectButtonClick);
    }

    private void OnNextButtonClick()
    {
        CharacterManager.Instance.NextCharacter();
    }

    private void OnPreviousButtonClick()
    {
        CharacterManager.Instance.PreviousCharacter();
    }

    private void OnSelectButtonClick()
    {
        CharacterManager.Instance.SelectCharacter();
    }

    // Thêm phím tắt để điều khiển
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            OnNextButtonClick();
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            OnPreviousButtonClick();
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            OnSelectButtonClick();
        }
        else if (Input.GetKeyDown(KeyCode.Tab))
        {
            CharacterManager.Instance.ShowCharacterSelection();
        }
    }
}
