# Homebrew formula for easel.
# Install: brew install lukoplt/tap/easel  (after tapping the release repo)
class Easel < Formula
  desc "Static analysis for Power Apps canvas source (pa.yaml)"
  homepage "https://github.com/lukoplt/easel"
  version "0.1.5"
  license "MIT"

  on_macos do
    url "https://github.com/lukoplt/easel/releases/download/v0.1.5/easel-osx-arm64.tar.gz"
    sha256 "399b684021b7c66c8d96c27f88a57703ef4e4f8f7635fde606c3d5fbe12eb51e"
  end

  on_linux do
    url "https://github.com/lukoplt/easel/releases/download/v0.1.5/easel-linux-x64.tar.gz"
    sha256 "e9d9de5a7b9c530e55bb71c4e88faa949f64017f965b0e2b417c2059a0af3a7a"
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
