using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PillePille
{
    public sealed class PreviewCapture : MonoBehaviour
    {
        IEnumerator Start()
        {
            var game=GetComponent<PilleGame>();var args=Environment.GetCommandLineArgs();
            bool audit=args.Contains("-pilleNetAudit");
            if(audit&&game.authority) {
                float deadline=Time.realtimeSinceStartup+30;
                while(game.simulation.players.Count<3&&Time.realtimeSinceStartup<deadline)yield return null;
                yield return new WaitForSeconds(2);
                game.StartRound();Debug.Log("PILLE_NET_HOST_STARTED players="+game.simulation.players.Count);
            }
            yield return new WaitForSeconds(audit?7:2);
            string label=args.Contains("-pilleClient")?"cliente":game.session?"juego":"menu";
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath,"../captura_"+label+".png")));
            // Offscreen capture is also available when a hidden/batch player has no presentable backbuffer.
            var camera=Camera.main;var rt=new RenderTexture(1280,720,24);
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../arena_"+label+".png")),image.EncodeToPNG());
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;rt.Release();Destroy(rt);Destroy(image);
            if(audit) {
                Debug.Log("PILLE_NET_SNAPSHOT "+JsonUtility.ToJson(game.view));
                yield return new WaitForSeconds(game.authority?125:8);
                Debug.Log("PILLE_NET_FINAL "+JsonUtility.ToJson(game.view));
            } else yield return new WaitForSeconds(1);
            Application.Quit();
        }
    }
}
