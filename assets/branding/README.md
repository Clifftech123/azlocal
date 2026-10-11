# azlocal branding

The terminal mark represents running Azure SDK workflows locally. The artwork belongs to azlocal and does not use Microsoft's Azure logo.

| Asset | Use |
| --- | --- |
| `logo.svg` | Scalable logo source |
| `icon.png` | 128 × 128 transparent package icon |
| `banner.svg` | Scalable banner source |
| `banner.png` | 1200 × 360 banner for GitHub and NuGet |

The palette uses Azure blue (`#0078D4`), cyan (`#50E6FF`), and navy (`#071B32`). PNG exports provide compatible images for package listings. Keep both formats in sync when changing the artwork.

`NUGET_README.md` is the reader-facing package documentation packed into all three NuGet packages. Its banner uses an absolute GitHub raw URL because NuGet does not render relative image paths. The repository README can use a relative path.
