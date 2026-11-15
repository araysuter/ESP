# Independent image check for the Sagittarius view emitted by -espCelestialSkySmoke.
# Run on the rendering machine with OpenImageIO 3.2.2.0 and numpy.
import sys,datetime,math,numpy as np,OpenImageIO as oi
source=oi.ImageBuf(sys.argv[1]).get_pixels(oi.FLOAT)[:,:,:3]
actual=oi.ImageBuf(sys.argv[2]).get_pixels(oi.FLOAT)[:,:,:3]
epoch=datetime.datetime(2000,1,1,12); days=(datetime.datetime(2026,11,15)-epoch).total_seconds()/86400;t=days/36525
sid=(280.46061837+360.98564736629*days+.000387933*t*t-t*t*t/38710000)%360*math.pi/180
asec=math.pi/648000
zeta=(2306.2181*t+.30188*t*t+.017998*t*t*t)*asec;z=(2306.2181*t+1.09468*t*t+.018203*t*t*t)*asec;theta=(2004.3109*t-.42665*t*t-.041833*t*t*t)*asec
def rz(a): return np.array([[math.cos(a),-math.sin(a),0],[math.sin(a),math.cos(a),0],[0,0,1]])
ry=np.array([[math.cos(theta),0,-math.sin(theta)],[0,1,0],[math.sin(theta),0,math.cos(theta)]])
ecef=rz(z-sid)@ry@rz(zeta);world=ecef[[0,2,1],:]
ra=math.radians(266.4);dec=math.radians(-28.94)
fwd=world@np.array([math.cos(dec)*math.cos(ra),math.cos(dec)*math.sin(ra),math.sin(dec)])
right=np.cross([0,1,0],fwd);right/=np.linalg.norm(right);up=np.cross(fwd,right)
y,x=np.mgrid[100:700:4,100:1340:4];h,w=actual.shape[:2]
rays=fwd+((x+.5)*2/w-1)[...,None]*math.tan(math.pi/8)*w/h*right+(1-(y+.5)*2/h)[...,None]*math.tan(math.pi/8)*up
rays=rays@world;rays/=np.linalg.norm(rays,axis=2)[...,None]
u=(.5-np.arctan2(rays[:,:,1],rays[:,:,0])/(2*math.pi))%1
v=.5+np.arcsin(rays[:,:,2])/math.pi
observed=actual[y,x].mean(axis=2).ravel()
for name,vv in [('north-up',1-v),('inverted',v)]:
    expected=source[np.minimum((vv*source.shape[0]).astype(int),source.shape[0]-1),(u*source.shape[1]).astype(int)]
    expected=np.where(expected<=.0031308,expected*12.92,1.055*np.maximum(expected,0)**(1/2.4)-.055)
    print(name,'luminance correlation',np.corrcoef(expected.mean(axis=2).ravel(),observed)[0,1])
