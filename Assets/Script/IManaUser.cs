using UnityEngine;

public interface IManaUser
{
    float CurrentMana { get; set; }
    float MaxMana { get; }
    void ResetMana();
}
