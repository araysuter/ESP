using System;
using System.Collections;
using UnityEngine;
namespace ESP.Presentation
{
    public sealed class LocationInspector : MonoBehaviour
    {
        GlobePointPicker picker;PointWeather weather;NasaRegionalPreview region;NasaMapLayers layers;
        bool visible=true;
        IEnumerator Start()
        {
            picker=GetComponent<GlobePointPicker>();weather=GetComponent<PointWeather>();region=GetComponent<NasaRegionalPreview>();layers=GetComponent<NasaMapLayers>();
            picker.PointSelected+=Selected;layers.LayerChanged+=RefreshImage;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-espInspectTest")>=0){picker.Select(42.28,-83.74);yield return new WaitForSecondsRealtime(30);ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(Application.persistentDataPath,"esp-inspector.png"));}
            while(true){yield return new WaitForSecondsRealtime(600);if(picker.HasSelection)weather.Fetch(picker.Latitude,picker.Longitude);}
        }
        void Selected(double lat,double lon){visible=true;weather.Fetch(lat,lon);RefreshImage();}
        void RefreshImage(){if(picker!=null&&picker.HasSelection)region.Fetch(picker.Latitude,picker.Longitude,layers.DisplayedLayerId,layers.DisplayedDate);}
        void Preset(double lat,double lon){picker.Select(lat,lon);GetComponent<GlobeCameraControls>().Focus(lat,lon);}
        void OnGUI()
        {
            if(picker==null)return;
            visible=GUI.Toggle(new Rect(20,135,330,24),visible,"Point inspection — right-click the globe");if(!visible)return;
            GUI.Box(new Rect(20,165,340,365),GUIContent.none);
            if(GUI.Button(new Rect(30,175,95,25),"Michigan"))Preset(42.28,-83.74);
            if(GUI.Button(new Rect(130,175,95,25),"Starbase"))Preset(25.997,-97.156);
            if(GUI.Button(new Rect(230,175,115,25),"Southern Ocean"))Preset(-50,120);
            if(!picker.HasSelection){GUI.Label(new Rect(30,215,310,55),"Select any visible point for regional NASA imagery and modeled point weather.");return;}
            GUI.Label(new Rect(30,207,315,22),$"Selected {picker.Latitude:F2}°, {picker.Longitude:F2}°");
            var style=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=12};
            GUI.Label(new Rect(30,233,315,35),weather.Status,style);
            GUI.Label(new Rect(30,267,315,25),"Valid time: "+weather.validtime,style);
            GUI.Label(new Rect(30,292,315,58),weather.Details,style);
            if(region.Image)GUI.DrawTexture(new Rect(30,355,145,120),region.Image,ScaleMode.ScaleToFit);
            GUI.Label(new Rect(183,355,162,120),region.Status,style);
            GUI.Label(new Rect(30,480,315,40),"Weather: Open-Meteo (model, not NASA). Imagery day is independent of current weather. No flight forces.",style);
        }
        void OnDestroy(){if(picker!=null)picker.PointSelected-=Selected;if(layers!=null)layers.LayerChanged-=RefreshImage;}
    }
}
