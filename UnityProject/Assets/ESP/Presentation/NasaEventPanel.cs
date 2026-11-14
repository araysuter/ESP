using UnityEngine;
namespace ESP.Presentation
{
    public sealed class NasaEventPanel : MonoBehaviour
    {
        NasaEventFeed feed;
        int drawn=-1;
        GameObject markerRoot;
        bool visible=true;
        void Start(){feed=GetComponent<NasaEventFeed>();markerRoot=new GameObject("NASA EONET point locations");}
        void Update()
        {
            if(feed==null||feed.Entries.Count==drawn)return;
            drawn=feed.Entries.Count;
            foreach(Transform child in markerRoot.transform)Destroy(child.gameObject);
            foreach(var item in feed.Entries){
                double lat=item.Latitude*System.Math.PI/180,lon=item.Longitude*System.Math.PI/180;
                double n=6378137/System.Math.Sqrt(1-0.00669437999014*System.Math.Pow(System.Math.Sin(lat),2));
                var o=GameObject.CreatePrimitive(PrimitiveType.Sphere);o.name=item.Title;o.transform.SetParent(markerRoot.transform);
                o.transform.position=new Vector3((float)((n+50000)*System.Math.Cos(lat)*System.Math.Cos(lon)/1e6),(float)((n*(1-.00669437999014)+50000)*System.Math.Sin(lat)/1e6),(float)((n+50000)*System.Math.Cos(lat)*System.Math.Sin(lon)/1e6));o.transform.localScale=Vector3.one*.09f;
                Destroy(o.GetComponent<Collider>());var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=new Color(1,.45f,.08f);o.GetComponent<Renderer>().material=m;
            }
        }
        void OnGUI()
        {
            var space=GetComponent<NasaSpaceWeather>();
            if(space!=null){GUI.Box(new Rect(Screen.width-355,250,335,100),GUIContent.none);GUI.Label(new Rect(Screen.width-343,260,310,85),"NASA space weather / last 7 days\n"+space.Status);}
            float x=Screen.width-355;GUI.Box(new Rect(x,20,335,220),GUIContent.none);
            visible=GUI.Toggle(new Rect(x+12,28,310,24),visible,"EONET open-event snapshot (30 days)");if(markerRoot)markerRoot.SetActive(visible);
            GUI.Label(new Rect(x+12,55,310,36),feed==null?"Loading events":feed.Status);
            if(feed!=null)for(int i=0;i<Mathf.Min(3,feed.Entries.Count);i++){var e=feed.Entries[i];GUI.Label(new Rect(x+12,90+i*35,310,35),e.Title+"\n"+e.Date);}
            GUI.Label(new Rect(x+12,200,310,30),"Max 20 reports; dated points, not storm extents");
        }
        void OnDestroy(){if(markerRoot){foreach(var r in markerRoot.GetComponentsInChildren<Renderer>())Destroy(r.sharedMaterial);Destroy(markerRoot);}}
    }
}
