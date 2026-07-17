# Homebrew formula for easel. sha256 values are filled at release time.
# Install: brew install easel/tap/easel  (after tapping the release repo)
class Pacheck < Formula
  desc "Static analysis for Power Apps canvas source (pa.yaml)"
  homepage "https://github.com/easel/easel"
  version "0.1.0"
  license "MIT"

  on_macos do
    url "https://github.com/easel/easel/releases/download/v0.1.0/easel-osx-arm64.tar.gz"
    sha256 "0000000000000000000000000000000000000000000000000000000000000000"
  end

  on_linux do
    url "https://github.com/easel/easel/releases/download/v0.1.0/easel-linux-x64.tar.gz"
    sha256 "0000000000000000000000000000000000000000000000000000000000000000"
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
