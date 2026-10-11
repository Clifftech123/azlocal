# azlocal branding

The terminal mark represents running Azure SDK workflows locally. The artwork belongs to azlocal and does not use Microsoft's Azure logo.

| Asset | Use |
| --- | --- |
| `logo.svg` | Scalable logo source |
| `icon.png` | 128 × 128 transparent package icon |
| `banner.svg` | Legacy asset path serving the same logo |
| `banner.png` | Legacy image URL serving the 128 × 128 logo for older package READMEs |

The palette uses Azure blue (`#0078D4`), cyan (`#50E6FF`), and navy (`#071B32`). PNG exports provide compatible images for package listings. Keep both formats in sync when changing the artwork.

`NUGET_README.md` is the reader-facing package documentation packed into all three NuGet packages. Its logo uses an absolute GitHub raw URL because NuGet does not render relative image paths. The repository README can use a relative path. Keep the legacy banner URLs available for already published packages; they now display the logo instead of a banner.

## Package badges

Until NuGet publication, the READMEs show CI status, a release-pending badge, and the license. NuGet version and download endpoints return "not found" for unpublished packages.

After publishing and confirming NuGet indexing, replace the pending badge in both READMEs with these live badges:

```markdown
[![CLI version](https://img.shields.io/nuget/vpre/azlocal?label=CLI&color=0078D4)](https://www.nuget.org/packages/azlocal)
[![Client version](https://img.shields.io/nuget/vpre/AzLocal.Client?label=Client&color=0078D4)](https://www.nuget.org/packages/AzLocal.Client)
[![CLI downloads](https://img.shields.io/nuget/dt/azlocal?label=CLI%20downloads&color=0078D4)](https://www.nuget.org/packages/azlocal)
[![Client downloads](https://img.shields.io/nuget/dt/AzLocal.Client?label=Client%20downloads&color=0078D4)](https://www.nuget.org/packages/AzLocal.Client)
```
