using UdonSharp;
using UnityEngine;

namespace TextureFireWorks
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class FireworkButton : UdonSharpBehaviour
    {
        public FireworkLauncher launcher;

        public override void Interact()
        {
            if (launcher != null) launcher.RequestLaunch();
        }
    }
}
