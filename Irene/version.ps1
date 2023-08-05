mkdir -p secrets
git log -1 --format="%H" > secrets/git-commit.txt
git describe --tags --abbrev=0 > secrets/git-tag.txt
