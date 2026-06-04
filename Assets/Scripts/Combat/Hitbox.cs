using UnityEngine;

public class Hitbox : MonoBehaviour
{
    public enum HitPart
    {
        Body,
        Head
    }

    [SerializeField] private HitPart part = HitPart.Body;
    [SerializeField] private float damageMultiplier = 1f;

    public HitPart Part => part;
    public float DamageMultiplier => damageMultiplier;
    public bool IsHeadshot => part == HitPart.Head;
}
