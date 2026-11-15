using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Networking;
namespace ESP.Presentation
{
    public sealed class NasaMapLayers : MonoBehaviour
    {
        public bool showLegacyUI = true;
        public Renderer earth;
        public Texture offlineTexture;
        public Texture cloudLegend;
        // Serialize this reference in scene builders so player shader stripping retains it.
        public Shader cloudShader;
        public Texture CloudDensityTexture => cloudDownloaded;
        public Shader CloudShader => cloudShader ? cloudShader : Shader.Find("ESP/ObservedCloudShell");
        public bool CloudsVisible => displayed == 1 && cloudDownloaded;
        CloudShell clouds;
        public string[] layerIds={"MODIS_Terra_CorrectedReflectance_TrueColor","MODIS_Terra_Cloud_Fraction_Day"};
        public string[] layerNames={"Satellite true color","Cloud fraction"};
        string[] defaults;
        string[] timeDomains;
        bool metadataLoading;
        UnityWebRequest activeRequest;
        string status="Loading NASA layer availability…", shown="Offline Blue Marble composite";
        string requestedDate="";
        int selected=1, displayed=-1, generation; // Start with the latest observed clouds; Blue Marble remains the offline fallback.
        Texture2D downloaded, cloudDownloaded;
        sealed class CachedLayer
        {
            public Texture2D Texture;
            public DateTime RequestedDay, First, Last;
            public string LayerId, Description, Status;
        }
        CachedLayer[] cachedLayers;
        int loadingIndex=-1;
        bool available;
        DateTime observationFirst, observationLast;
        public string Status=>status;
        // Compute against the wall clock so retained observations age even when a refresh fails.
        public string Freshness => displayed < 0 ? "Offline historical surface" :
            NasaImageryTime.Describe(observationFirst, observationLast, DateTime.UtcNow);
        public int SelectedIndex=>selected;
        public int DisplayedIndex=>displayed;
        public bool IsLoading=>loadingIndex>=0 || (metadataLoading && selected>=0);
        static string ModeName(int index)=>index<0?"SURFACE":index==0?"SATELLITE":"CLOUDS";
        public string SelectionStatus=>selected!=displayed
            ? "Showing "+ModeName(displayed)+" • "+(IsLoading?"loading ":"requested ")+ModeName(selected)
            : IsLoading?"Refreshing "+ModeName(selected)+"…":"";
        public string DisplayedDescription=>shown;
        public string DisplayedLayerId { get; private set; }="BlueMarble_ShadedRelief_Bathymetry";
        public string DisplayedDate { get; private set; }="";
        public event Action LayerChanged;
        bool autoRefresh=true;
        IEnumerator AutoRefresh(){while(true){yield return new WaitForSecondsRealtime(900);if(!autoRefresh)continue;yield return StartCoroutine(Availability());}}
        void LateUpdate()
        {
            Shader.SetGlobalTexture("_EspObservedClouds", cloudDownloaded ? cloudDownloaded : Texture2D.blackTexture);
            Shader.SetGlobalFloat("_EspObservedCloudsEnabled", CloudsVisible ? 1 : 0);
        }
        void OnDisable() { Shader.SetGlobalFloat("_EspObservedCloudsEnabled", 0); }
        IEnumerator Start(){defaults=new string[layerIds.Length];timeDomains=new string[layerIds.Length];cachedLayers=new CachedLayer[layerIds.Length];yield return StartCoroutine(Availability());StartCoroutine(AutoRefresh());}

        public void RefreshLatest()
        {
            requestedDate="";
            if(!metadataLoading && defaults!=null) StartCoroutine(Availability());
        }
        IEnumerator Availability()
        {
            if(metadataLoading)yield break;
            metadataLoading=true;
            bool parsedMetadata=false;
            using(var req=UnityWebRequest.Get("https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetCapabilities"))
            {
                req.timeout=20;yield return req.SendWebRequest();
                metadataLoading=false;
                if(req.result!=UnityWebRequest.Result.Success){status="NASA unavailable — previous map retained; retry a layer to reconnect";yield break;}
                try{
                    var doc=XDocument.Parse(req.downloadHandler.text);
                    for(int i=0;i<layerIds.Length;i++){
                        var layer=doc.Descendants().FirstOrDefault(e=>e.Name.LocalName=="Layer"&&e.Elements().Any(n=>n.Name.LocalName=="Name"&&n.Value==layerIds[i]));
                        var dim=layer?.Elements().FirstOrDefault(e=>e.Name.LocalName=="Dimension"&&(string)e.Attribute("name")=="time");
                        timeDomains[i]=dim?.Value;
                        // The default is advisory. Select the newest explicitly advertised day
                        // at/before UTC today, respecting gaps and interval cadence.
                        var latest=NasaImageryTime.LatestDay(timeDomains[i],DateTime.UtcNow);
                        defaults[i]=latest.HasValue?NasaImageryTime.FormatDay(latest.Value):null;
                    }
                    parsedMetadata=true;available=true;status="NASA availability retrieved "+DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'",CultureInfo.InvariantCulture);
                    Debug.Log("ESP_NASA_CAPABILITIES_OK "+string.Join(",",defaults));
                }catch(Exception){status="NASA metadata could not be read — previous map retained";}
            }
            if(!parsedMetadata)yield break;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-espLiveTest")>=0&&available)Select(0);
            else if(selected>=0&&available)Select(selected,true);
        }
        static bool TryDay(string value,out DateTime date)=>DateTime.TryParseExact(value,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out date);
        bool Advertised(int index,DateTime day)
        {
            return NasaImageryTime.Contains(timeDomains[index],day);
        }
        public void Select(int index, bool forceReload=false)
        {
            if(index < -1 || index>=layerIds.Length)return;
            // Repeated clicks on a loading mode must not starve its request.
            if(!forceReload && selected==index && loadingIndex==index)return;
            generation++;selected=index;
            activeRequest?.Abort();activeRequest=null;loadingIndex=-1;
            if(index<0){if(clouds)clouds.Hide();displayed=-1;earth.material.mainTexture=offlineTexture;shown="Offline Blue Marble composite — historical fallback, not current observations";status="Bundled fallback — not current weather";DisplayedLayerId="BlueMarble_ShadedRelief_Bathymetry";DisplayedDate="";LayerChanged?.Invoke();return;}
            var cached=cachedLayers!=null?cachedLayers[index]:null;
            // Keep texture, dates and provenance together. A cached cloud view can
            // reappear immediately after Surface without depending on the network.
            bool restored=cached!=null && cached.LayerId==layerIds[index] && Display(index,cached,true);
            if(restored)LayerChanged?.Invoke();
            if(!available){status="Waiting for NASA availability…";if(!metadataLoading&&defaults!=null)StartCoroutine(Availability());return;}
            string date=string.IsNullOrWhiteSpace(requestedDate)?defaults[index]:requestedDate.Trim();
            if(!TryDay(date,out var parsed)||parsed>DateTime.UtcNow.Date||!Advertised(index,parsed)){status="No verified date — previous map retained; use an advertised YYYY-MM-DD, no future dates";return;}
            if(restored && !forceReload && cached.RequestedDay==parsed)return;
            loadingIndex=index;
            StartCoroutine(Load(index,parsed,generation));
        }
        bool Display(int index,CachedLayer layer,bool fromCache)
        {
            if(index==1){
                if(!clouds)clouds=earth.GetComponent<CloudShell>();
                if(!clouds)clouds=earth.gameObject.AddComponent<CloudShell>();
                if(!clouds.Show(layer.Texture,CloudShader))return false;
                earth.material.mainTexture=offlineTexture;
                cloudDownloaded=layer.Texture;
            }else{
                if(clouds)clouds.Hide();
                earth.material.mainTexture=layer.Texture;downloaded=layer.Texture;
            }
            observationFirst=layer.First;observationLast=layer.Last;
            DisplayedLayerId=layer.LayerId;DisplayedDate=NasaImageryTime.FormatDay(layer.Last);displayed=index;
            shown=layer.Description;status=layer.Status+(fromCache?" • cached observations":"");
            return true;
        }
        Texture2D OfflineCopy()
        {
            var rt=RenderTexture.GetTemporary(2048,1024,0,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            try{
                Graphics.Blit(offlineTexture,rt);RenderTexture.active=rt;
                var copy=new Texture2D(2048,1024,TextureFormat.RGBA32,false);
                copy.ReadPixels(new Rect(0,0,2048,1024),0,0);copy.Apply();return copy;
            }finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
        IEnumerator Load(int index,DateTime newest,int id)
        {
            bool cloudLayer=index==1;
            // Satellite gaps retain Blue Marble; cloud gaps stay transparent above that base.
            // Cloud scalar values are decoded before any spatial interpolation.
            var result=cloudLayer?new Texture2D(2048,1024,TextureFormat.RGBA32,true,true):OfflineCopy();
            var pixels=cloudLayer?new Color32[2048*1024]:result.GetPixels32();
            var covered=new bool[pixels.Length];
            var cloudBlend=cloudLayer?new CloudShell.ObservationBlend(2048,1024):null;
            DateTime first=DateTime.MaxValue,last=DateTime.MinValue;int successes=0,validPixels=0,unknownPixels=0;
            try{
                int days=3;
                for(int offset=0;offset<days;offset++){
                    var day=newest.AddDays(-offset);if(!Advertised(index,day))continue;
                    string date=NasaImageryTime.FormatDay(day);
                    status="Loading NASA "+layerNames[index]+" "+date+" ("+(offset+1)+"/"+days+")…";
                    string url="https://gibs.earthdata.nasa.gov/wms/epsg4326/best/wms.cgi?SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS="+UnityWebRequest.EscapeURL(layerIds[index])+"&STYLES=&CRS=EPSG%3A4326&BBOX=-90%2C-180%2C90%2C180&WIDTH=2048&HEIGHT=1024&FORMAT=image%2Fpng&TRANSPARENT=TRUE&TIME="+date;
                    using(var req=UnityWebRequestTexture.GetTexture(url)){
                        activeRequest=req;req.timeout=20;yield return req.SendWebRequest();
                        if(activeRequest==req)activeRequest=null;
                        if(id!=generation)yield break;
                        if(req.result!=UnityWebRequest.Result.Success)continue;
                        var texture=DownloadHandlerTexture.GetContent(req);
                        if(texture.width!=2048||texture.height!=1024){Destroy(texture);continue;}
                        var source=texture.GetPixels32();Destroy(texture);
                        Color32[] mask=null;
                        if(index==0){
                            // Native MODIS JPEG tiles retain opaque black in PNG output.
                            // NASA's paired no-data mask is opaque only where observations are absent.
                            string maskUrl=url.Replace(UnityWebRequest.EscapeURL(layerIds[index]),"MODIS_Terra_Data_No_Data");
                            using(var maskRequest=UnityWebRequestTexture.GetTexture(maskUrl)){
                                activeRequest=maskRequest;maskRequest.timeout=20;yield return maskRequest.SendWebRequest();
                                if(activeRequest==maskRequest)activeRequest=null;
                                if(id!=generation)yield break;
                                if(maskRequest.result!=UnityWebRequest.Result.Success)continue;
                                var maskTexture=DownloadHandlerTexture.GetContent(maskRequest);
                                if(maskTexture.width==2048&&maskTexture.height==1024)mask=maskTexture.GetPixels32();
                                Destroy(maskTexture);if(mask==null)continue;
                            }
                        }
                        bool any=false;
                        if(cloudLayer){
                            int observed=cloudBlend.Add(source,offset,covered,out int unknown);
                            unknownPixels+=unknown;any=observed>0;
                            Debug.Log("ESP_CLOUD_OBSERVATION date="+date+" valid="+observed+" unknown="+unknown);
                        }
                        else for(int p=0;p<pixels.Length;p++){
                            var c=source[p];if(covered[p]||c.a==0||(mask!=null&&mask[p].a>0))continue;
                            // Respect NASA's no-data mask, never infer observation coverage from RGB.
                            pixels[p]=(Color32)Color.Lerp((Color)pixels[p],(Color)c,c.a/255f);
                            covered[p]=true;any=true;validPixels++;
                        }
                        if(any){successes++;if(day<first)first=day;if(day>last)last=day;}
                    }
                }
                if(id!=generation)yield break;
                if(successes==0){status="NASA imagery unavailable/empty — previous map retained";yield break;}
                if(cloudLayer){pixels=cloudBlend.Finish();validPixels=covered.Count(value=>value);}
                result.SetPixels32(pixels);result.Apply(cloudLayer,false);
                result.wrapModeU=TextureWrapMode.Repeat;result.wrapModeV=TextureWrapMode.Clamp;
                result.filterMode=cloudLayer?FilterMode.Trilinear:FilterMode.Bilinear;
                string dateRange=NasaImageryTime.FormatDay(first)+(first!=last?" to "+NasaImageryTime.FormatDay(last):"");
                double coverage=AreaCoverage(covered,2048,1024);
                var next=new CachedLayer {
                    Texture=result,RequestedDay=newest,First=first,Last=last,LayerId=layerIds[index],
                    Description=(cloudLayer?"Smoothed cloud observation blend":"Satellite true-color temporal mosaic")+" "+dateRange+" UTC • "+(cloudLayer?"Blue Marble ground; transparent unsampled areas; 10 km display height":"Blue Marble fills no-data")+"; not instantaneous",
                    Status="NASA GIBS • "+dateRange+" UTC • "+coverage.ToString("F1")+"% area observed / "+(100-coverage).ToString("F1")+"% missing • fetched "+DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'",CultureInfo.InvariantCulture)+" • "+(cloudLayer?"smoothed dated cloud blend":"historical fallback in gaps")
                };
                if(!Display(index,next,false)){status="Cloud renderer unavailable — previous map retained";yield break;}
                var previous=cachedLayers[index];cachedLayers[index]=next;result=null;
                if(previous!=null && previous.Texture)Destroy(previous.Texture);
                loadingIndex=-1;
                LayerChanged?.Invoke();
                Debug.Log("ESP_NASA_LAYER_OK "+layerIds[index]+" "+DisplayedDate);
                if(cloudLayer)Debug.Log("ESP_CLOUD_SHELL_OK valid="+validPixels+" missing="+(pixels.Length-validPixels)+" observedAreaPercent="+coverage.ToString("F3")+" unknown="+unknownPixels+" days="+successes+" range="+dateRange+" composite=normalized-observation-blend base=BlueMarble");
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-espLiveTest")>=0)StartCoroutine(CaptureTest(index));
            }finally{if(result)Destroy(result);if(id==generation)loadingIndex=-1;}
        }
        static double AreaCoverage(bool[] covered,int width,int height)
        {
            double observed=0,total=0;
            for(int y=0;y<height;y++){
                double area=Math.Cos(((y+.5)/height-.5)*Math.PI);
                for(int x=0;x<width;x++){total+=area;if(covered[y*width+x])observed+=area;}
            }
            return 100*observed/total;
        }
        void OnGUI()
        {
            if (!showLegacyUI) return;
            float y=Screen.height-170;GUI.Box(new Rect(16,y,Screen.width-32,154),GUIContent.none);
            GUI.Label(new Rect(30,y+8,Screen.width-60,22),shown+" • "+Freshness);
            if(GUI.Button(new Rect(30,y+35,130,28),"Offline globe"))Select(-1);
            GUI.enabled=true;
            for(int i=0;i<layerNames.Length;i++)if(GUI.Button(new Rect(170+i*185,y+35,175,28),layerNames[i]))Select(i);
            GUI.enabled=true;
            GUI.Label(new Rect(30,y+70,110,22),"Date (optional)");requestedDate=GUI.TextField(new Rect(140,y+70,120,24),requestedDate,10);
            if(GUI.Button(new Rect(270,y+70,140,24),"Refresh latest"))RefreshLatest();
            if(GUI.Button(new Rect(420,y+70,90,24),"Load date")&&selected>=0)Select(selected);
            if(displayed==1)GUI.Label(new Rect(560,y+35,330,65),"White = smoothed dated cloud fractions\nTransparent = clear or unsampled areas\nRepresentative display height: 10 km");
            autoRefresh=GUI.Toggle(new Rect(900,y+40,240,25),autoRefresh,"Refresh latest imagery every 15 min");
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
        void OnDestroy(){activeRequest?.Abort();StopAllCoroutines();if(cachedLayers!=null)foreach(var cached in cachedLayers)if(cached!=null && cached.Texture)Destroy(cached.Texture);}
    }
}
