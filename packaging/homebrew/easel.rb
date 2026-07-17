# Homebrew formula for easel.
# Install: brew install lukoplt/tap/easel  (after tapping the release repo)
class Easel < Formula
  desc "Static analysis for Power Apps canvas source (pa.yaml)"
  homepage "https://github.com/lukoplt/easel"
  version "0.1.4"
  license "MIT"

  on_macos do
    url "https://github.com/lukoplt/easel/releases/download/v0.1.4/easel-osx-arm64.tar.gz"
    sha256 "8e97c188495a6bac59e49870c27d4b477146b90a681dbec5b4a52b649e5e15b1"
  end

  on_linux do
    url "https://github.com/lukoplt/easel/releases/download/v0.1.4/easel-linux-x64.tar.gz"
    sha256 "ddc1ba91cfe2c0c3dbc403430a7386b6aa51c664bf0798781bae0a24ac293ea7"
  end

  def install
    bin.install "easel"
  end

  def caveats
    <<~EOS
      easel needs the Power Platform CLI (pac) to read .msapp files:
        dotnet tool install --global Microsoft.PowerApps.CLI.Tool
    EOS
  end

  test do
    assert_match "easel", shell_output("#{bin}/easel --version")
  end
end
