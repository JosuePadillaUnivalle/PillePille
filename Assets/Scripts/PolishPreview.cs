using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PillePille
{
    // Opt-in visual test fixture. Ordinary launches never create or change this scene.
    public sealed class PolishPreview : MonoBehaviour
    {
        IEnumerator Start()
        {
            var game=GetComponent<PilleGame>();
            game.SetMode(MatchMode.Infection);game.Host(true);game.StartRound();
            var sim=game.simulation;
            var local=sim.players[game.localId];
            var hunter=sim.players.Values.First(p=>p.state.tagged);
            if(hunter==local) {
                hunter=sim.players.Values.First(p=>p!=local);local.state.tagged=false;hunter.state.tagged=true;
            }
            var captive=sim.players.Values.First(p=>p!=local&&p!=hunter);
            foreach(var player in sim.players.Values) {
                player.state.bot=false;player.state.safeSeconds=0;
                player.ResetAt(new Vector3(13,1.05f,13));
            }
            local.ResetAt(new Vector3(-2,1.05f,-12));local.transform.rotation=Quaternion.Euler(0,180,0);
            captive.ResetAt(new Vector3(-.2f,1.05f,-12));
            hunter.ResetAt(new Vector3(14,1.05f,-12));
            sim.TryTag(hunter.state.id,captive.state.id);
            sim.message="";game.view=sim.Snapshot();
            Debug.Log("PILLE_POLISH_PREVIEW survivor="+local.state.id+" trapped="+captive.state.id+" infected="+hunter.state.id);
            yield return new WaitForSeconds(1);
            // Render the actual procedural characters for asset QA, independent of the game HUD.
            var positions=sim.players.Values.ToDictionary(p=>p,p=>p.transform.position);
            local.ResetAt(new Vector3(-2,1.05f,-12));
            captive.ResetAt(new Vector3(0,1.05f,-12));
            hunter.ResetAt(new Vector3(2,1.05f,-12));
            foreach(var player in new[]{local,captive,hunter}) {player.transform.rotation=Quaternion.Euler(0,180,0);player.Visual(false,-1);}
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;
            var target=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);
            var textRenderers=FindObjectsOfType<TextMesh>().Select(t=>t.GetComponent<Renderer>()).ToArray();
            foreach(var text in textRenderers)text.enabled=false;
            camera.transform.position=new Vector3(0,2.5f,-16.5f);camera.transform.LookAt(new Vector3(0,1.2f,-12));
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var picture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            picture.ReadPixels(new Rect(0,0,1280,720),0,0);picture.Apply();
            File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../pulido_personajes.png")),picture.EncodeToPNG());
            camera.targetTexture=target;RenderTexture.active=active;camera.transform.position=position;camera.transform.rotation=rotation;
            foreach(var text in textRenderers)text.enabled=true;
            rt.Release();Destroy(rt);Destroy(picture);
            foreach(var pair in positions)pair.Key.ResetAt(pair.Value);
        }
    }
}
