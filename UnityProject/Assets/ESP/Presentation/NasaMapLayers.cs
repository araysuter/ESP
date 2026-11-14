using System;
using System.Collections;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Networking;
namespace ESP.Presentation
{
    public sealed class NasaMapLayers : MonoBehaviour
    {
        public Renderer earth;
        public Texture offlineTexture;
        public Texture cloudLegend;
        public string[] layerIds={"MODIS_Terra_CorrectedReflectance_TrueColor","MODIS_Terra_Cloud_Fraction_Day"};
        public string[] layerNames={"Satellite true color","Cloud fraction"};
        string[] defaults=new string[2];
        string status="Loading NASA layer availability…", shown="Offline Blue Marble composite";
        string requestedDate="";
        int selected=-1, displayed=-1, generation;
        Texture2D downloaded;
        bool available;
        public string Status=>status;
        IEnumerator Start()
        {
            using(var req=UnityWebRequest.Get("https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetCapabilities"))
            {
                req.timeout=20;yield return req.SendWebRequest();
                if(req.result!=UnityWebRequest.Result.Success){status="NASA unavailable — bundled offline map retained";yield break;}
                try{
                    var doc=XDocument.Parse(req.downloadHandler.text);
                    for(int i=0;i<layerIds.Length;i++){
                        var layer=doc.Descendants().FirstOrDefault(e=>e.Name.LocalName=="Layer"&&e.Elements().Any(n=>n.Name.LocalName=="Name"&&n.Value==layerIds[i]));
                        var dim=layer?.Elements().FirstOrDefault(e=>e.Name.LocalName=="Dimension"&&(string)e.Attribute("name")=="time");
                        defaults[i]=(string)dim?.Attribute("default");
                        if(DateTime.TryParse(defaults[i],out var day)&&day.Date>DateTime.UtcNow.Date)defaults[i]=DateTime.UtcNow.ToString("yyyy-MM-dd");
                    }
                    available=true;status="NASA availability retrieved "+DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'");
                    Debug.Log("ESP_NASA_CAPABILITIES_OK "+string.Join(",",defaults));
                }catch(Exception){status="NASA metadata could not be read — offline map retained";}
            }
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-espLiveTest")>=0&&available)Select(0);
        }
        public void Select(int index)
        {
            generation++;selected=index;
            if(index<0){displayed=-1;earth.material.mainTexture=offlineTexture;shown="Offline Blue Marble composite";status="Bundled fallback — not current weather";return;}
            if(!available||string.IsNullOrEmpty(defaults[index])){status="This layer has no verified available date";return;}
            string date=string.IsNullOrWhiteSpace(requestedDate)?defaults[index]:requestedDate.Trim();
            if(!DateTime.TryParseExact(date.Substring(0,Math.Min(10,date.Length)),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var parsed)||parsed.Date>DateTime.UtcNow.Date){status="Enter an available date as YYYY-MM-DD (no future dates)";return;}
            StartCoroutine(Load(index,date,generation));
        }
        IEnumerator Load(int index,string date,int id)
        {
            status="Requesting "+layerNames[index]+" for "+date+"…";
            string url="https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS="+UnityWebRequest.EscapeURL(layerIds[index])+"&STYLES=&CRS=EPSG%3A4326&BBOX=-90%2C-180%2C90%2C180&WIDTH=2048&HEIGHT=1024&FORMAT=image%2Fjpeg&TIME="+UnityWebRequest.EscapeURL(date);
            using(var req=UnityWebRequestTexture.GetTexture(url)){
                req.timeout=30;yield return req.SendWebRequest();
                if(id!=generation)yield break;
                if(req.result!=UnityWebRequest.Result.Success){status="Layer unavailable for requested date — previous map retained";yield break;}
                var texture=DownloadHandlerTexture.GetContent(req);
                if(texture.width!=2048||texture.height!=1024){Destroy(texture);status="Unexpected NASA response — previous map retained";yield break;}
                texture.wrapModeU=TextureWrapMode.Repeat;texture.wrapModeV=TextureWrapMode.Clamp;
                earth.material.mainTexture=texture;if(downloaded)Destroy(downloaded);downloaded=texture;
                displayed=index;shown=layerNames[index]+" | requested observation day "+date;
                status="NASA GIBS • fetched "+DateTime.UtcNow.ToString("HH:mm 'UTC'")+" • gaps/partial coverage possible";
                Debug.Log("ESP_NASA_LAYER_OK "+layerIds[index]+" "+date);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-espLiveTest")>=0)StartCoroutine(CaptureTest(index));
            }
        }
        void OnGUI()
        {
            float y=Screen.height-170;GUI.Box(new Rect(16,y,Screen.width-32,154),GUIContent.none);
            GUI.Label(new Rect(30,y+8,Screen.width-60,22),shown);
            if(GUI.Button(new Rect(30,y+35,130,28),"Offline globe"))Select(-1);
            GUI.enabled=available;
            for(int i=0;i<layerNames.Length;i++)if(GUI.Button(new Rect(170+i*185,y+35,175,28),layerNames[i]))Select(i);
            GUI.enabled=true;
            GUI.Label(new Rect(30,y+70,110,22),"Date (optional)");requestedDate=GUI.TextField(new Rect(140,y+70,120,24),requestedDate,10);
            if(GUI.Button(new Rect(270,y+70,140,24),"Latest date")){requestedDate="";if(selected>=0)Select(selected);}
            if(GUI.Button(new Rect(420,y+70,90,24),"Load date")&&selected>=0)Select(selected);
            if(displayed==1&&cloudLegend)GUI.DrawTexture(new Rect(560,y+35,300,65),cloudLegend,ScaleMode.ScaleToFit);
            GUI.Label(new Rect(30,y+100,Screen.width-60,22),status);
            GUI.Label(new Rect(30,y+125,Screen.width-60,22),"Drag to orbit • Scroll to zoom • Satellite composites are not live wind/forecast data; no flight forces applied.");
        }
        IEnumerator CaptureTest(int index)
        {
            yield return new WaitForSeconds(3);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath,"esp-live-"+index+".png"));
            yield return new WaitForSeconds(3);
            if(index==0)Select(1);
            else { Select(-1);Debug.Log("ESP_NASA_OFFLINE_RESTORE_OK"); }
        }
        void OnDestroy(){if(downloaded)Destroy(downloaded);}
    }
}
