# CI integration

## GitHub Actions (bundled composite action)

Lint on every pull request and upload SARIF to GitHub code scanning:

```yaml
# .github/workflows/easel.yml
name: easel
on: [pull_request]

permissions:
  contents: read
  security-events: write   # required to upload SARIF

jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Run easel
        uses: lukoplt/easel/action@v0     # or a pinned SHA
        with:
          path: ./src/MyApp          # unpacked pa.yaml folder or a .msapp
          format: sarif
          fail-on: warning
          output: easel.sarif

      - name: Upload SARIF
        if: always()
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: easel.sarif
```

The action installs `pac` and `easel`, runs `easel lint`, and fails the job when
findings reach `fail-on`. Findings appear inline on the PR via code scanning.

## dotnet tool (any CI)

```bash
dotnet tool install --global Microsoft.PowerApps.CLI.Tool
dotnet tool install --global EaselCli
export PATH="$PATH:$HOME/.dotnet/tools"

easel lint ./src/MyApp --format sarif -o easel.sarif --fail-on warning
```

Exit code `1` means findings reached the threshold — the step fails and gates the build.

## Diff in a PR

Compare the app on the PR branch against the base branch and post the changelog as a
comment. The comment body is easel's own generated Markdown — no PR title/body is
interpolated into any `run:` step, so there is no injection surface.

```yaml
# .github/workflows/easel-diff.yml
name: easel diff
on: [pull_request]

permissions:
  contents: read
  pull-requests: write

jobs:
  diff:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout head
        uses: actions/checkout@v4
        with:
          path: head

      - name: Checkout base
        uses: actions/checkout@v4
        with:
          ref: ${{ github.event.pull_request.base.sha }}
          path: base

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet tool install --global EaselCli
      - run: echo "$HOME/.dotnet/tools" >> "$GITHUB_PATH"

      - name: Generate diff
        run: easel diff base/src/MyApp head/src/MyApp --format markdown -o diff.md

      - name: Comment on PR
        uses: actions/github-script@v7
        with:
          script: |
            const fs = require('fs');
            const body = fs.readFileSync('diff.md', 'utf8');
            await github.rest.issues.createComment({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.issue.number,
              body,
            });
```

Adjust `base/src/MyApp` and `head/src/MyApp` to your app's path.
