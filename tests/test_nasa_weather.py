import datetime as dt, importlib.util, math, pathlib, unittest
p=pathlib.Path(__file__).resolve().parents[1]/'scripts/fetch_nasa_profile.py'
spec=importlib.util.spec_from_file_location('fetch',p); f=importlib.util.module_from_spec(spec);spec.loader.exec_module(f)
UTC=dt.timezone.utc
class Tests(unittest.TestCase):
 def setUp(self):
  self.when=dt.datetime(2026,11,14,9,tzinfo=UTC)
  values='\n'.join(f'[0][{i}][0], {i+1}' for i in range(72))
  self.sample=f'h, [1][72][1][1]\n{values}\ntime, [1]\n{self.when.toordinal()+1+9/24}\nlev, [72]\n'+', '.join(map(str,range(1,73)))+'\nlat, [1]\n28.5\nlon, [1]\n-80.625\n'
 def test_coordinates(self):
  self.assertEqual(len(f.parse_response(self.sample,'h',self.when,28.5,-80.625)),72)
  for bad in (self.sample.replace('28.5','28.75'),self.sample.replace('-80.625','-80.5'),self.sample.replace('739935.375','739935.5'),self.sample.replace('1, 2, 3,','2, 2, 3,'),self.sample.replace('[0][2][0], 3','')):
   with self.assertRaises(ValueError):f.parse_response(bad,'h',self.when,28.5,-80.625)
 def test_time(self):
  start=self.when-dt.timedelta(hours=9);step=dt.timedelta(hours=3)
  self.assertEqual(f.select_time(start,self.when+step,step,self.when+dt.timedelta(hours=1))[0],self.when)
  with self.assertRaises(ValueError):f.select_time(start,self.when,step,self.when+dt.timedelta(hours=12,seconds=1))
  with self.assertRaises(ValueError):f.select_time(start,self.when,step,start-dt.timedelta(seconds=1))
 def test_fills(self):
  data={'h':[0,100,200,300],'pl':[100000,90000,80000,110000],'t':[280,270,1e15,260],'u':[1]*4,'v':[2]*4,'ps':[101000]}
  self.assertEqual(len(f.build_levels(data)),2)
  with self.assertRaises(ValueError):f.build_levels(data, minimum_levels=36)
  data['ps']=[1e15]
  with self.assertRaises(ValueError):f.build_levels(data)
 def test_nonfinite_coordinates(self):
  for x in (math.nan,math.inf,-math.inf):
   with self.assertRaises(ValueError):f.run(x,0)
if __name__=='__main__':unittest.main()
