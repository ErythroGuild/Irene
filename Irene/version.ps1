mkdir -p config
git log -1 --format="%H" > config/git-commit.txt
git describe --tags --abbrev=0 > config/git-tag.txt
