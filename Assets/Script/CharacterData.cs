using UnityEngine;
using System;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject
{
    public string characterName;
    public Sprite characterSprite;
    public GameObject characterPrefab;
    
    [Header("Stats")]
    public float baseHealth = 100f;
    public float baseMana = 100f;
    public float baseAttackDamage = 20f;
    public float baseMoveSpeed = 5f;
    
    [Header("Mana Costs")]
    public float attackManaCost = 10f;
    public float skillManaCost = 30f;
}
