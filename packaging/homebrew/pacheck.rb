# Homebrew formula for pacheck. sha256 values are filled at release time.
# Install: brew install pacheck/tap/pacheck  (after tapping the release repo)
class Pacheck < Formula
  desc "Static analysis for Power Apps canvas source (pa.yaml)"
  homepage "https://github.com/pacheck/pacheck"
  version "0.1.0"
  license "MIT"

  on_macos do
    url "https://github.com/pacheck/pacheck/releases/download/v0.1.0/pacheck-osx-arm64.tar.gz"
    sha256 "0000000000000000000000000000000000000000000000000000000000000000"
  end

  on_linux do
    url "https://github.com/pacheck/pacheck/releases/download/v0.1.0/pacheck-linux-x64.tar.gz"
    sha256 "0000000000000000000000000000000000000000000000000000000000000000"
  end

  def install
    bin.install "pacheck"
  end

  def caveats
    <<~EOS
      pacheck needs the Power Platform CLI (pac) to read .msapp files:
        dotnet tool install --global Microsoft.PowerApps.CLI.Tool
    EOS
  end

  test do
    assert_match "pacheck", shell_output("#{bin}/pacheck --version")
  end
end
