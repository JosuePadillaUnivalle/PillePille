using UnityEngine;

namespace PillePille
{
    public sealed class PlayerMotor : MonoBehaviour
    {
        public PlayerState state;
        public CharacterController controller;
        public InputFrame input = new InputFrame();
        public float lastInput;
        float vertical, dashLeft;
        Vector3 dashDirection, targetPosition;
        RunnerAvatar avatar;
        Vector3 lastVisualPosition;
        TextMesh label;
        TrailRenderer speedTrail;
        Material trailMaterial;
        public static PlayerMotor Create(PlayerState state, bool authority)
        {
            var go = new GameObject("Jugador_"+state.id);
            var motor = go.AddComponent<PlayerMotor>();
            motor.state=state; go.transform.position=state.position;
            motor.targetPosition=state.position;
            motor.controller=go.AddComponent<CharacterController>();
            motor.controller.height=2;motor.controller.radius=.5f;
            motor.controller.stepOffset=.3f;motor.controller.slopeLimit=48;motor.controller.skinWidth=.035f;
            motor.controller.enabled=authority;
            motor.avatar=RunnerAvatar.Create(go.transform,state.id);
            motor.lastVisualPosition=state.position;
            var text=new GameObject("Nombre");text.layer=2;text.transform.SetParent(go.transform,false);text.transform.localPosition=Vector3.up*1.28f;
            motor.label=text.AddComponent<TextMesh>();motor.label.fontSize=48;motor.label.characterSize=.052f;
            motor.label.anchor=TextAnchor.MiddleCenter;
            motor.label.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            motor.speedTrail=go.AddComponent<TrailRenderer>();
            motor.trailMaterial=new Material(Shader.Find("Sprites/Default"));
            motor.speedTrail.sharedMaterial=motor.trailMaterial;
            motor.speedTrail.time=.24f;motor.speedTrail.startWidth=.32f;motor.speedTrail.endWidth=0;
            motor.speedTrail.minVertexDistance=.12f;
            motor.speedTrail.numCornerVertices=3;
            motor.speedTrail.startColor=new Color(.65f,1,.15f,.8f);motor.speedTrail.endColor=new Color(.65f,1,.15f,0);
            motor.speedTrail.emitting=false;
            return motor;
        }
        public void ResetAt(Vector3 pos)
        {
            bool active=controller.enabled;controller.enabled=false;transform.position=pos;controller.enabled=active;
            state.position=pos;targetPosition=pos;vertical=0;dashLeft=0;state.dashCooldown=0;input=new InputFrame();
            lastVisualPosition=pos;
            if(speedTrail!=null)speedTrail.Clear();
        }
        public void Tick(float dt)
        {
            if (!state.alive) return;
            state.safeSeconds=Mathf.Max(0,state.safeSeconds-dt);
            state.dashCooldown=Mathf.Max(0,state.dashCooldown-dt);
            state.speedSeconds=Mathf.Max(0,state.speedSeconds-dt);
            var direction=Vector3.ClampMagnitude(new Vector3(input.x,0,input.z),1);
            if(state.trapped) {direction=Vector3.zero;dashLeft=0;}
            bool ground=controller.isGrounded;
            if (ground && vertical<0) vertical=-2;
            if (ground && input.jump && !state.trapped) vertical=Rules.JumpSpeed;
            if(input.dash && !state.trapped && state.dashCooldown<=0 && direction.sqrMagnitude>.01f) {
                dashLeft=Rules.DashDuration;dashDirection=direction.normalized;state.dashCooldown=Rules.DashRecharge;
            }
            vertical-=Rules.Gravity*dt;
            float speed=(state.tagged?Rules.ChaserSpeed:Rules.RunnerSpeed)*(state.speedSeconds>0?Rules.SpeedMultiplier:1f);
            var velocity=dashLeft>0 ? dashDirection*Rules.DashSpeed : direction*speed;
            dashLeft=Mathf.Max(0,dashLeft-dt);velocity.y=vertical;
            controller.Move(velocity*dt);
            if(direction.sqrMagnitude>.01f) transform.rotation=Quaternion.LookRotation(direction);
            state.position=transform.position;state.yaw=transform.eulerAngles.y;state.grounded=controller.isGrounded;
            if(transform.position.y<-8) ResetAt(ArenaBuilder.Spawns[state.id%ArenaBuilder.Spawns.Length]);
            input.jump=false;input.dash=false;
        }
        public void Apply(PlayerState update)
        {
            state=update;targetPosition=update.position;
            if(Vector3.Distance(transform.position,targetPosition)>5) transform.position=targetPosition;
        }
        public void Visual(bool remote,int localId)
        {
            if(remote) {
                transform.position=Vector3.Lerp(transform.position,targetPosition,1-Mathf.Exp(-22*Time.deltaTime));
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.Euler(0,state.yaw,0),1-Mathf.Exp(-20*Time.deltaTime));
            }
            float dt=Mathf.Max(Time.deltaTime,.001f);
            var displacement=transform.position-lastVisualPosition;
            // Actual displacement animates both authority players and interpolated LAN replicas.
            var visualVelocity=displacement.sqrMagnitude<16 ? displacement/dt : Vector3.zero;
            lastVisualPosition=transform.position;
            avatar.Visual(state,state.id==localId,visualVelocity,dashLeft>0||new Vector2(visualVelocity.x,visualVelocity.z).magnitude>12,dt);
            label.text=state.trapped?"+ "+Mathf.CeilToInt(state.trapSeconds)+" s":state.name+(state.id==localId?" [TÚ]":"");
            speedTrail.emitting=state.alive&&!state.trapped&&state.speedSeconds>0;
            label.color=state.trapped?new Color(1,.79f,.3f):state.id==localId?Color.white:new Color(.8f,.88f,.91f,.9f);
            if(Camera.main!=null)label.transform.rotation=Camera.main.transform.rotation;
        }
        void OnDestroy() { if(trailMaterial!=null)Destroy(trailMaterial); }
    }
}
