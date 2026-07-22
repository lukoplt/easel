# Homebrew formula for easel.
# Install: brew install lukoplt/tap/easel  (after tapping the release repo)
class Easel < Formula
  desc "Static analysis for Power Apps canvas source (pa.yaml)"
  homepage "https://github.com/lukoplt/easel"
  version "0.1.6"
  license "MIT"

  on_macos do
    url "https://github.com/lukoplt/easel/releases/download/v0.1.6/easel-osx-arm64.tar.gz"
    sha256 "d25b0be46398f90e26fdf43ab650ef6e4f17d9096bf42e5de9caf2b4f68f0b47"
  end

  on_linux do
    url "https://github.com/lukoplt/easel/releases/download/v0.1.6/easel-linux-x64.tar.gz"
    sha256 "d3562713069fe10cad95279db0f5b898b483a5fef4008e7817f1a5f2af45d3ea"
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
