using UnityEngine;

namespace Fun
{
    public class GangnamStyleSoundButton : MonoBehaviour
    {
        [SerializeField] private AudioClip[] audioClips;
        [SerializeField] private AudioSource audioSource;

        public void GangnamStyleSound()
        {
            Debug.Log("버튼 누름");
           int random =  Random.Range(0, audioClips.Length);

            audioSource.PlayOneShot(audioClips[random], GameSettings.SfxVolume);
        }
    }
}