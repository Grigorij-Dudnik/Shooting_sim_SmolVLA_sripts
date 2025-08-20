git add .
git commit -m "update"
git --force push

git push --delete origin v2.1
git tag -d v2.1
git tag v2.1
git push origin v2.1

rm -rf /home/gregor/.cache/huggingface/lerobot/Grigorij/Shooting_unit_2
