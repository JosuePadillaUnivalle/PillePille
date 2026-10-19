using UnityEngine;

namespace PillePille
{
    // Presentation only: all pickup and cooldown decisions belong to MatchSimulation.
    public sealed class SpeedPad : MonoBehaviour
    {
        public static readonly Vector3[] Positions = {
            new Vector3(-8,.025f,-13), new Vector3(8,.025f,12), new Vector3(12,.025f,-5)
        };
        static SpeedPad[] visuals;
        Material plateMaterial, arrowMaterial;
        TextMesh label;

        public static void EnsurePads()
        {
            if(visuals!=null&&visuals.Length==Positions.Length&&visuals[0]!=null)return;
            var root=GameObject.Find("Baldosas_Supervelocidad");
            if(root==null)root=new GameObject("Baldosas_Supervelocidad");
            visuals=new SpeedPad[Positions.Length];
            for(int i=0;i<Positions.Length;i++) {
                var pad=new GameObject("Impulso_"+(i+1));pad.transform.SetParent(root.transform,false);
                pad.transform.position=Positions[i];
                var visual=pad.AddComponent<SpeedPad>();visuals[i]=visual;
                visual.plateMaterial=ArenaBuilder.Material(new Color(.3f,.9f,.12f));
                visual.arrowMaterial=ArenaBuilder.Material(new Color(.9f,1,.8f));
                visual.Part("Baldosa",Vector3.zero,new Vector3(2.6f,.04f,2.6f),0,visual.plateMaterial);
                for(int n=0;n<3;n++) {
                    float z=-.75f+n*.65f;
                    visual.Part("Flecha_I",new Vector3(-.22f,.04f,z),new Vector3(.12f,.03f,.7f),45,visual.arrowMaterial);
                    visual.Part("Flecha_D",new Vector3(.22f,.04f,z),new Vector3(.12f,.03f,.7f),-45,visual.arrowMaterial);
                }
                var text=new GameObject("Indicador");text.transform.SetParent(pad.transform,false);text.transform.localPosition=Vector3.up*.65f;
                visual.label=text.AddComponent<TextMesh>();visual.label.fontSize=48;visual.label.characterSize=.055f;
                visual.label.anchor=TextAnchor.MiddleCenter;visual.label.alignment=TextAlignment.Center;
            }
            ApplyAll(null,false);
        }

        void Part(string partName,Vector3 position,Vector3 scale,float yaw,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=partName;
            part.layer=2;part.transform.SetParent(transform,false);part.transform.localPosition=position;
            part.transform.localScale=scale;part.transform.localRotation=Quaternion.Euler(0,yaw,0);
            Destroy(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;
        }

        public static void ApplyAll(PadState[] pads,bool playing)
        {
            if(visuals==null)return;
            for(int i=0;i<visuals.Length;i++) {
                var visual=visuals[i];if(visual==null)continue;
                float cooldown=0;
                if(pads!=null)foreach(var pad in pads)if(pad.id==i){cooldown=pad.cooldown;break;}
                bool ready=playing&&cooldown<=0;
                visual.plateMaterial.color=ready?new Color(.35f,.95f,.12f):new Color(.18f,.25f,.22f);
                visual.arrowMaterial.color=ready?new Color(.9f,1,.75f):new Color(.4f,.48f,.42f);
                visual.label.color=ready?new Color(.75f,1,.35f):new Color(.7f,.78f,.73f);
                visual.label.text="VELOCIDAD "+(i+1)+"\n"+(playing?(ready?"+50% / 10 s":Mathf.CeilToInt(cooldown)+" s / RECARGA"):"+50% / 10 s");
            }
        }

        void LateUpdate() {if(label!=null&&Camera.main!=null)label.transform.rotation=Camera.main.transform.rotation;}
        void OnDestroy() {if(plateMaterial!=null)Destroy(plateMaterial);if(arrowMaterial!=null)Destroy(arrowMaterial);}
    }
}
