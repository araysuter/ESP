import datetime as dt
import importlib.util
from pathlib import Path
import unittest
spec=importlib.util.spec_from_file_location('nasa_feed',Path(__file__).resolve().parents[1]/'scripts/nasa_feed.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class FeedTests(unittest.TestCase):
    now=dt.datetime(2026,11,15,12,tzinfo=dt.timezone.utc)
    def test_empty_is_valid_catalog_not_all_clear(self):
        self.assertEqual(m.feed([],self.now)['items'],[])
    def test_allowlist_strips_unknown_fields(self):
        result=m.feed([dict(startTime='2026-11-15T00:00Z',api_key='private',allKpIndex=[dict(kpIndex=5,observedTime='2026-11-15T03:00Z',secret='private')])],self.now)
        self.assertNotIn('private',str(result));self.assertEqual(result['items'][0]['allKpIndex'][0]['kpIndex'],5)
    def test_error_object_rejected(self):
        with self.assertRaises(ValueError):m.feed({'error':'rate limit'},self.now)
    def test_bad_values_rejected(self):
        for value in (float('nan'),float('inf'),-1,10,True):
            with self.subTest(value=value),self.assertRaises(ValueError):
                m.feed([dict(startTime='2026-11-15T00:00Z',allKpIndex=[dict(kpIndex=value,observedTime='2026-11-15T03:00Z')])],self.now)
    def test_future_storm_rejected(self):
        with self.assertRaises(ValueError):m.feed([dict(startTime='2026-11-16T00:00Z')],self.now)
    def test_timezone_required(self):
        with self.assertRaises(ValueError):m.feed([dict(startTime='2026-11-15T00:00')],self.now)
if __name__=='__main__':unittest.main()
