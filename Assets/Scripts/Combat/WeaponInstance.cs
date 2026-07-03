using UnityEngine;

public class WeaponInstance : MonoBehaviour
{
    [Header("Runtime References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Animator[] linkedAnimators;
    [SerializeField] private PlayerAnimation playerAnimation;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Transform shellEjectPoint;
    [SerializeField] private ParticleSystem[] muzzleParticles;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameObject shellPrefab;

    public Animator Animator => animator;
    public Animator[] LinkedAnimators => linkedAnimators;
    public PlayerAnimation PlayerAnimation => playerAnimation;
    public Transform MuzzlePoint => muzzlePoint;
    public Transform ShellEjectPoint => shellEjectPoint;
    public ParticleSystem[] MuzzleParticles => muzzleParticles;
    public AudioSource AudioSource => audioSource;
    public GameObject ShellPrefab => shellPrefab;

    private void Awake()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        if (playerAnimation == null)
            playerAnimation = GetComponentInChildren<PlayerAnimation>(true);

        if (animator == null)
            animator = playerAnimation != null ? playerAnimation.GetComponent<Animator>() : GetComponentInChildren<Animator>(true);

        if (linkedAnimators == null || linkedAnimators.Length == 0)
            linkedAnimators = FindLinkedAnimators();

        if (audioSource == null)
            audioSource = GetComponentInChildren<AudioSource>(true);

        if (muzzlePoint == null)
            muzzlePoint = FindChildByName("Point", "MuzzlePoint", "Muzzle");

        if (shellEjectPoint == null)
            shellEjectPoint = FindChildByName("ShellEjectPoint", "Shell Eject Point", "EjectPoint");

        if (muzzleParticles == null || muzzleParticles.Length == 0)
            muzzleParticles = FindMuzzleParticles();
    }

    private Transform FindChildByName(params string[] candidates)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            for (int j = 0; j < candidates.Length; j++)
            {
                if (string.Equals(children[i].name, candidates[j], System.StringComparison.OrdinalIgnoreCase))
                    return children[i];
            }
        }

        return null;
    }

    private Animator[] FindLinkedAnimators()
    {
        Animator[] animators = GetComponentsInChildren<Animator>(true);
        if (animators == null || animators.Length <= 1)
            return System.Array.Empty<Animator>();

        System.Collections.Generic.List<Animator> results = new System.Collections.Generic.List<Animator>();
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate == null || candidate == animator)
                continue;

            results.Add(candidate);
        }

        return results.ToArray();
    }

    private ParticleSystem[] FindMuzzleParticles()
    {
        ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
        if (particles == null || particles.Length == 0)
            return System.Array.Empty<ParticleSystem>();

        System.Collections.Generic.List<ParticleSystem> muzzleResults = new System.Collections.Generic.List<ParticleSystem>();
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
                continue;

            string particleName = particle.name;
            if (particleName.IndexOf("Muzzle", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                particleName.IndexOf("Flash", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                muzzleResults.Add(particle);
            }
        }

        return muzzleResults.Count > 0 ? muzzleResults.ToArray() : particles;
    }
}
