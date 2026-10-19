using UnityEngine;

namespace PillePille
{
    public static class ArenaBuilder
    {
        static Material concrete, dark, light, stripe;
        public static readonly Vector3[] Spawns = {
            new Vector3(-12, 1.1f, -12), new Vector3(12, 1.1f, 12),
            new Vector3(12, 1.1f, -12), new Vector3(-12, 1.1f, 12),
            new Vector3(0, 1.1f, -13), new Vector3(0, 1.1f, 13),
            new Vector3(-13, 1.1f, 0), new Vector3(13, 1.1f, 0)
        };
        public static Material Material(Color color)
        {
            var source=Resources.Load<Material>("Graybox");
            var mat = source!=null?new Material(source):new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Glossiness", .12f);
            return mat;
        }
        public static void Build()
        {
            if (GameObject.Find("Arena_Graybox") != null) return;
            var root = new GameObject("Arena_Graybox").transform;
            concrete = Material(new Color(.42f, .46f, .48f));
            dark = Material(new Color(.20f, .24f, .27f));
            light = Material(new Color(.63f, .67f, .68f));
            stripe = Material(new Color(.86f, .72f, .35f));
            Box(root, "Suelo_36x36", new Vector3(0,-.5f,0), new Vector3(36,1,36), concrete);
            Box(root, "Muro_Norte", new Vector3(0,1.5f,18), new Vector3(37,3,1), dark);
            Box(root, "Muro_Sur", new Vector3(0,1.5f,-18), new Vector3(37,3,1), dark);
            Box(root, "Muro_Este", new Vector3(18,1.5f,0), new Vector3(1,3,36), dark);
            Box(root, "Muro_Oeste", new Vector3(-18,1.5f,0), new Vector3(1,3,36), dark);
            for (int i=-16;i<=16;i+=2) {
                Box(root, "Cuadricula_2m", new Vector3(i,.006f,0), new Vector3(.025f,.01f,35), light, false);
                Box(root, "Cuadricula_2m", new Vector3(0,.006f,i), new Vector3(35,.01f,.025f), light, false);
            }
            // Elevated cross with two climbable ramps and open routes underneath.
            Box(root,"Pasarela_Central",new Vector3(0,2.7f,0),new Vector3(14,.6f,4),light);
            Box(root,"Soporte_Oeste",new Vector3(-5,1.3f,0),new Vector3(1,2.6f,3),dark);
            Box(root,"Soporte_Este",new Vector3(5,1.3f,0),new Vector3(1,2.6f,3),dark);
            Ramp(root,"Rampa_Oeste",new Vector3(-10,1.3f,0),new Vector3(7,.4f,4),25);
            Ramp(root,"Rampa_Este",new Vector3(10,1.3f,0),new Vector3(7,.4f,4),-25);
            Box(root,"Isla_Norte",new Vector3(0,1.5f,7),new Vector3(5,3,4),light);
            Box(root,"Salto_Intermedio",new Vector3(0,.55f,11),new Vector3(3,1.1f,2),concrete);
            Box(root,"Cobertura_Sur_A",new Vector3(-5,1.25f,-8),new Vector3(2,2.5f,6),dark);
            Box(root,"Cobertura_Sur_B",new Vector3(5,1.25f,-8),new Vector3(2,2.5f,6),dark);
            Box(root,"Salto_Sur",new Vector3(0,.45f,-6),new Vector3(5,.9f,1),light);
            Box(root,"Cobertura_NO",new Vector3(-10,1.25f,8),new Vector3(5,2.5f,1.5f),dark);
            Box(root,"Cobertura_NE",new Vector3(10,1.25f,8),new Vector3(5,2.5f,1.5f),dark);
            Box(root,"Salto_SO",new Vector3(-11,.45f,-8),new Vector3(3,.9f,2),light);
            Box(root,"Salto_SE",new Vector3(11,.45f,-8),new Vector3(3,.9f,2),light);
            for (int i=0;i<Spawns.Length;i++) {
                var p=Spawns[i]; p.y=.02f;
                Box(root,"Salida_"+(i+1),p,new Vector3(2,.025f,2),stripe,false);
            }
            Label(root,"PILLE / PILLE",new Vector3(0,3.1f,17.45f),1.1f);
            Label(root,"01 / PATIO",new Vector3(-17.45f,3.2f,1),.48f,90);
            Label(root,"02 / PASARELA",new Vector3(0,3.8f,2),.38f);
            Label(root,"03 / ESCAPE",new Vector3(0,.045f,-14),.6f,0,90);
        }
        static GameObject Box(Transform root,string name,Vector3 pos,Vector3 scale,Material mat,bool collision=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name; go.transform.SetParent(root); go.transform.position=pos; go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=mat;
            if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static void Ramp(Transform root,string name,Vector3 pos,Vector3 scale,float angle)
        {
            var go=Box(root,name,pos,scale,concrete);
            go.transform.rotation=Quaternion.Euler(0,0,angle);
        }
        static void Label(Transform root,string text,Vector3 pos,float size,float yaw=0,float pitch=0)
        {
            var go=new GameObject(text); go.transform.SetParent(root);go.transform.position=pos;
            go.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            var tm=go.AddComponent<TextMesh>();tm.text=text;tm.fontSize=64;tm.characterSize=size/6;
            tm.anchor=TextAnchor.MiddleCenter;tm.color=new Color(.9f,.93f,.94f);
        }
    }
}
