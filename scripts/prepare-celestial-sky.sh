#!/bin/sh
# Run on the rendering/build machine, outside the Unity Assets directory.
# Requires curl, sha256sum or shasum, and OpenImageIO 3.2.2.0 oiiotool.
set -eu
source_dir=${1:?Usage: prepare-celestial-sky.sh SOURCE_DIR OUTPUT_EXR}
output=${2:?Provide the derived EXR output path}
mkdir -p "$source_dir"
base=https://svs.gsfc.nasa.gov/vis/a000000/a004800/a004851
for file in milkyway_2020_32k.exr hiptyc_2020_32k.exr; do
  curl -fLsS --retry 3 -C - -o "$source_dir/$file" "$base/$file"
done
(cd "$source_dir" && printf '%s\n' \
 'a697b3579b74206551d5b5f4d257e796dbcb0b6050621f4a4a4605bd22bd5976  milkyway_2020_32k.exr' \
 '53ca7df6be1664e4cca699c4bec0f8286e813ab131ca923ca71294e577706134  hiptyc_2020_32k.exr' | shasum -a 256 -c -)
# Reduce each linear layer before adding to avoid keeping both 32K buffers in RAM.
# Box averaging preserves integrated light; no baked tone mapping or gamma transform.
oiiotool --threads 4 --autotile 256 --cache 1024 "$source_dir/milkyway_2020_32k.exr" \
 --resize:filter=box 8192x4096 -d half -o "$source_dir/milkyway_8k.exr"
oiiotool --threads 4 --autotile 256 --cache 1024 "$source_dir/hiptyc_2020_32k.exr" \
 --resize:filter=box 8192x4096 "$source_dir/milkyway_8k.exr" --add --ch R,G,B \
 -d half --compression dwaa:100 -o "$output"
