using System.Collections.Generic;
using UnityEngine;
using FocusPark.Core;

namespace FocusPark.Game
{
    public sealed class ParkView : MonoBehaviour
    {
        public Camera ViewCamera { get; private set; }
        Transform buildings, preview;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        static readonly Color Grass = new Color(.48f,.73f,.55f), Cream = new Color(1f,.91f,.72f), Peach = new Color(.96f,.44f,.34f), Navy = new Color(.13f,.24f,.31f), Mint = new Color(.55f,.84f,.72f);
        public void Initialize()
        {
            RenderSettings.ambientLight = new Color(.72f,.81f,.82f);
            RenderSettings.fog = false;
            var light = new GameObject("Afternoon sun").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(45,-30,0);
            light.shadows = LightShadows.Soft;
            ViewCamera = new GameObject("Park camera").AddComponent<Camera>();
            ViewCamera.tag = "MainCamera"; ViewCamera.orthographic = true; ViewCamera.orthographicSize = 10.8f;
            ViewCamera.backgroundColor = new Color(.68f,.85f,.88f); ViewCamera.clearFlags = CameraClearFlags.SolidColor;
            ViewCamera.transform.position = new Vector3(17,18,-17); ViewCamera.transform.LookAt(new Vector3(0,0,0));
            Shape("Island foundation", PrimitiveType.Cube, new Vector3(0,-.7f,0), new Vector3(14,1.2f,14), new Color(.27f,.54f,.5f), transform);
            Shape("Lawn", PrimitiveType.Cube, new Vector3(0,-.09f,0), new Vector3(13,.25f,13), Grass, transform);
            Shape("Water", PrimitiveType.Plane, new Vector3(0,-1.4f,0), new Vector3(12,1,12), new Color(.51f,.77f,.82f), transform);
            for (int x=0;x<ParkRules.Size;x++) for (int z=0;z<ParkRules.Size;z++)
            {
                bool path = x==5 || z==5;
                var col = path ? Cream : ((x+z)%2==0 ? Grass : Grass*1.03f);
                Shape("Tile "+x+","+z, PrimitiveType.Cube, new Vector3(x-5.5f,.06f,z-5.5f), new Vector3(.96f,.08f,.96f), col, transform);
            }
            // Entrance and perimeter planting are scenery, outside the buildable grid.
            Shape("Gate left",PrimitiveType.Cube,new Vector3(-1.6f,1,-6.5f),new Vector3(.25f,2,.25f),Peach,transform);
            Shape("Gate right",PrimitiveType.Cube,new Vector3(.6f,1,-6.5f),new Vector3(.25f,2,.25f),Peach,transform);
            Shape("Gate sign",PrimitiveType.Cube,new Vector3(-.5f,2.05f,-6.5f),new Vector3(2.8f,.65f,.2f),Navy,transform);
            var sign = new GameObject("FOCUS PARK").AddComponent<TextMesh>();
            sign.text="FOCUS PARK"; sign.fontSize=64; sign.characterSize=.048f; sign.anchor=TextAnchor.MiddleCenter; sign.color=Cream;
            sign.transform.SetParent(transform); sign.transform.position=new Vector3(-.5f,2.05f,-6.62f); sign.transform.rotation=Quaternion.Euler(0,180,0);
            for(int i=0;i<7;i++) { Tree(new Vector3(-6.25f,0,-4.8f+i*1.7f),transform); Tree(new Vector3(6.25f,0,-4.8f+i*1.7f),transform); }
            for(int i=0;i<5;i++) Tree(new Vector3(-4.8f+i*2.4f,0,6.3f),transform);
            buildings=new GameObject("Your attractions").transform; buildings.SetParent(transform);
        }
        public void Frame(bool portrait)
        {
            if (ViewCamera==null) return;
            ViewCamera.rect = portrait ? new Rect(0,.43f,1,.57f) : new Rect(.30f,0,.70f,1);
            ViewCamera.orthographicSize = portrait ? 11.4f : 10.8f;
        }
        public void Refresh(ParkState state)
        {
            foreach(Transform child in buildings) Destroy(child.gameObject);
            foreach(var item in state.attractions)
            {
                var spec=ParkRules.Find(item.kind); if(spec==null) continue;
                int w=item.rotation%2==0 ? spec.Width:spec.Depth, d=item.rotation%2==0 ? spec.Depth:spec.Width;
                var root=new GameObject(spec.Name).transform; root.SetParent(buildings);
                root.position=new Vector3(item.x-6+w*.5f,.15f,item.z-6+d*.5f); root.rotation=Quaternion.Euler(0,item.rotation*90,0);
                Build(item.kind,root);
            }
        }
        public void Preview(string kind,int x,int z,int rotation,bool valid)
        {
            if(preview!=null) Destroy(preview.gameObject);
            if(string.IsNullOrEmpty(kind)) return;
            var spec=ParkRules.Find(kind); int w=rotation%2==0?spec.Width:spec.Depth, d=rotation%2==0?spec.Depth:spec.Width;
            preview=Shape("Placement outline",PrimitiveType.Cube,new Vector3(x-6+w*.5f,.22f,z-6+d*.5f),new Vector3(w-.05f,.10f,d-.05f),valid?new Color(.94f,.83f,.38f):Peach,transform).transform;
        }
        void Build(string kind,Transform root)
        {
            if(kind=="tree") { Tree(Vector3.zero,root); return; }
            if(kind=="carousel")
            {
                Shape("Round platform",PrimitiveType.Cylinder,new Vector3(0,.12f,0),new Vector3(1.8f,.12f,1.8f),Cream,root);
                Shape("Center pole",PrimitiveType.Cylinder,new Vector3(0,.85f,0),new Vector3(.12f,.75f,.12f),Navy,root);
                var canopy=Shape("Candy canopy",PrimitiveType.Sphere,new Vector3(0,1.5f,0),new Vector3(1.9f,.5f,1.9f),Peach,root);
                Shape("Canopy trim",PrimitiveType.Cylinder,new Vector3(0,1.38f,0),new Vector3(1.95f,.05f,1.95f),Cream,root);
                Shape("Top jewel",PrimitiveType.Sphere,new Vector3(0,1.86f,0),new Vector3(.2f,.25f,.2f),Cream,root);
                var spinning=new GameObject("Carousel seats").transform; spinning.SetParent(root,false); spinning.gameObject.AddComponent<ParkMotion>().speed=14;
                for(int i=0;i<6;i++)
                {
                    float a=i*Mathf.PI/3; var p=new Vector3(Mathf.Cos(a)*.62f,.75f,Mathf.Sin(a)*.62f);
                    Shape("Seat pole",PrimitiveType.Cylinder,p,new Vector3(.035f,.55f,.035f),Cream,spinning);
                    Shape("Little horse",PrimitiveType.Capsule,p+new Vector3(0,-.12f,0),new Vector3(.2f,.15f,.34f),i%2==0?Mint:Cream,spinning);
                }
                return;
            }
            if(kind=="stall")
            {
                Shape("Soda shop",PrimitiveType.Cube,new Vector3(0,.4f,0),new Vector3(.85f,.8f,.8f),Mint,root);
                Shape("Shop canopy",PrimitiveType.Cube,new Vector3(0,.93f,-.08f),new Vector3(1,.2f,1),Peach,root);
                Shape("Counter",PrimitiveType.Cube,new Vector3(0,.58f,-.43f),new Vector3(.9f,.1f,.25f),Cream,root);
                Shape("Window",PrimitiveType.Cube,new Vector3(0,.65f,-.405f),new Vector3(.55f,.3f,.02f),Navy,root);
                return;
            }
            Shape("Wheel base",PrimitiveType.Cube,new Vector3(0,.1f,0),new Vector3(2.8f,.2f,1.8f),Cream,root);
            for(int side=-1;side<=1;side+=2)
            {
                var leg=Shape("Wheel support",PrimitiveType.Cube,new Vector3(side*.55f,1.3f,0),new Vector3(.17f,2.6f,.18f),Peach,root);
                leg.transform.localRotation=Quaternion.Euler(0,0,side*23);
            }
            var wheel=new GameObject("Turning wheel").transform; wheel.SetParent(root,false); wheel.localPosition=new Vector3(0,2.4f,0);
            var motion=wheel.gameObject.AddComponent<ParkMotion>(); motion.axis=Vector3.forward; motion.speed=7;
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                var spoke=Shape("Spoke",PrimitiveType.Cube,Vector3.zero,new Vector3(.055f,2.6f,.055f),Cream,wheel);
                spoke.transform.localRotation=Quaternion.Euler(0,0,i*30);
                Shape("Passenger cabin",PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*1.35f,Mathf.Sin(a)*1.35f,0),new Vector3(.34f,.36f,.4f),i%2==0?Peach:Mint,wheel);
            }
        }
        void Tree(Vector3 p,Transform parent)
        {
            Shape("Tree trunk",PrimitiveType.Cylinder,p+new Vector3(0,.35f,0),new Vector3(.13f,.35f,.13f),new Color(.55f,.39f,.25f),parent);
            Shape("Tree crown",PrimitiveType.Sphere,p+new Vector3(0,.92f,0),new Vector3(.74f,1.05f,.74f),Mint*.85f,parent);
        }
        GameObject Shape(string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color,Transform parent)
        {
            var obj=GameObject.CreatePrimitive(type); obj.name=name; obj.transform.SetParent(parent,false); obj.transform.localPosition=position; obj.transform.localScale=scale;
            if(!materials.TryGetValue(color,out var material)) { material=new Material(Shader.Find("Standard")); material.color=color; material.SetFloat("_Glossiness",.15f); materials[color]=material; }
            obj.GetComponent<Renderer>().sharedMaterial=material;
            var collider=obj.GetComponent<Collider>(); if(collider!=null) Destroy(collider);
            return obj;
        }
    }
}
