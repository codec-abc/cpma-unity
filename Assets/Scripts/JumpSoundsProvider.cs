using UnityEngine;

public class JumpSoundsProvider : MonoBehaviour
{
    [SerializeField]
    AudioClip[] jumpSounds;

    public AudioClip[] GetJumpSounds()
    {
        return jumpSounds;
    }
}
