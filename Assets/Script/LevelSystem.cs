using UnityEngine;
using UnityEngine.Events;

public class LevelSystem : MonoBehaviour
{
    [SerializeField] private int level = 1;
    [SerializeField] private int experience = 0;
    [SerializeField] private int experienceToNextLevel = 100;
    [SerializeField] private float experienceMultiplier = 1.5f;

    public UnityEvent onLevelUp;
    public UnityEvent<int> onExperienceGained;

    public int Level => level;
    public int Experience => experience;
    public int ExperienceToNextLevel => experienceToNextLevel;

    public void AddExperience(int amount)
    {
        experience += amount;
        onExperienceGained?.Invoke(amount);

        while (experience >= experienceToNextLevel)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        level++;
        experience -= experienceToNextLevel;
        experienceToNextLevel = Mathf.RoundToInt(experienceToNextLevel * experienceMultiplier);
        onLevelUp?.Invoke();
    }
}