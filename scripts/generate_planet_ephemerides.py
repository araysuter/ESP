"""Generate the same ESP1 offline vector format for the supplied solar-system bodies."""
import generate_celestial_ephemeris as ep
from pathlib import Path
import uuid
BODIES={'Mercury':'199','Venus':'299','Mars':'499','Jupiter':'599','Saturn':'699','Uranus':'799','Neptune':'899','Pluto':'999','Ceres':'1;','Eris':'136199;','Haumea':'136108;','Makemake':'136472;'}
if __name__=='__main__':
    for name,target in BODIES.items():
        ep.write(target,'1 d',24,'SolarSystem/'+name+'Ephemeris.bytes')
        path=ep.ROOT/'UnityProject/Assets/ESP/Resources/SolarSystem'/ (name+'Ephemeris.bytes.meta')
        if not path.exists(): path.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
