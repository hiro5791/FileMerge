<#
.SYNOPSIS
    Builds the Partner Center listing import folder for every language.

.DESCRIPTION
    Takes the CSV exported from Partner Center ("Export listings") as a template, fills in the
    text from docs/store/listing-text.json and the screenshots from
    build/Capture-StoreScreenshots.ps1, and writes an import folder:

        artifacts/store-listing/listingData.csv
        artifacts/store-listing/images/<code>-light.png, <code>-dark.png

    In Partner Center, choose "Import listings" and select that folder.

    Button and option names inside the text come from the app's own string tables, so the
    listing always matches what the user sees in the app.

.PARAMETER Template
    The CSV exported from Partner Center for this submission.

.PARAMETER LocalizedJapaneseTitle
    Use the Japanese app name as the Japanese listing title. The name must be reserved for the
    product in Partner Center (App management > Manage app names) before importing.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $Template,
    [string] $OutDir,
    [string] $Copyright = "$([char]0x00A9) 2026 Hiroyura",
    [string] $Developer = 'Hiroyura',
    [switch] $LocalizedJapaneseTitle
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not $OutDir) { $OutDir = Join-Path $repoRoot 'artifacts\store-listing' }
$imagesDir = Join-Path $OutDir 'images'
$stringsDir = Join-Path $repoRoot 'src\FileMerge\Localization\Strings'
$text = Get-Content (Join-Path $repoRoot 'docs\store\listing-text.json') -Raw -Encoding UTF8 | ConvertFrom-Json

# Partner Center column -> app language code
$columns = [ordered]@{
    'en-us' = 'en'; 'ja-jp' = 'ja'; 'zh-hans' = 'zh-Hans'; 'zh-hant' = 'zh-Hant'; 'ko-kr' = 'ko'
    'es-es' = 'es'; 'pt-br' = 'pt-BR'; 'fr-fr' = 'fr'; 'de-de' = 'de'; 'it-it' = 'it'
    'ru-ru' = 'ru'; 'uk-ua' = 'uk'; 'pl-pl' = 'pl'; 'nl-nl' = 'nl'; 'sv-se' = 'sv'
    'tr-tr' = 'tr'; 'ar-sa' = 'ar'; 'hi-in' = 'hi'; 'id-id' = 'id'; 'vi-vn' = 'vi'; 'th-th' = 'th'
}

$problems = New-Object System.Collections.Generic.List[string]

function Expand([string] $s, $strings, [string] $title) {
    $s.Replace('{T}', $title).
       Replace('{AddFiles}', $strings.'Action.AddFiles').
       Replace('{AddFolder}', $strings.'Action.AddFolder').
       Replace('{Merge}', $strings.'Action.Merge').
       Replace('{Donate}', $strings.'Action.Donate').
       Replace('{Opt1}', $strings.'Options.EnsureTrailingNewline').
       Replace('{Opt2}', $strings.'Options.RemoveInnerBoms')
}

# Last paragraph of every description: a link to the page listing the developer's apps.
function MoreApps([string] $code) {
    $label = $text._moreApps.labels.$code
    if (-not $label) { $problems.Add("$code has no more-apps label") }
    $link = $text._moreApps.links.$code
    if (-not $link) { $link = $text._moreApps.links.default }
    "$label`r`n$link"
}

# Build every language's values first, then write them into the template rows.
$values = @{}
foreach ($column in $columns.Keys) {
    $code = $columns[$column]
    $entry = $text.$code
    if ($null -eq $entry) { $problems.Add("$code has no listing text"); continue }

    $strings = Get-Content (Join-Path $stringsDir "$code.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    $title = if ($code -eq 'ja' -and $LocalizedJapaneseTitle) { $strings.'App.Title' } else { 'TekuTeku File Merge' }

    $v = @{
        Title = $title
        ShortDescription = Expand $entry.short $strings $title
        Description = (Expand ($entry.description -join "`r`n") $strings $title) + "`r`n`r`n" + (MoreApps $code)
        ReleaseNotes = $entry.notes
        DevStudio = $Developer
        CopyrightTrademarkInformation = $Copyright
    }

    for ($i = 0; $i -lt $entry.features.Count; $i++) { $v["Feature$($i + 1)"] = Expand $entry.features[$i] $strings $title }
    for ($i = 0; $i -lt $entry.terms.Count; $i++) { $v["SearchTerm$($i + 1)"] = $entry.terms[$i] }

    $shots = @("$code-light.png", "$code-dark.png")
    for ($i = 0; $i -lt $shots.Count; $i++) {
        if (-not (Test-Path (Join-Path $imagesDir $shots[$i]))) { $problems.Add("missing screenshot images/$($shots[$i])") }
        $v["DesktopScreenshot$($i + 1)"] = "images/$($shots[$i])"
    }

    # Partner Center limits
    if ($v.Description.Length -gt 10000) { $problems.Add("$code description is over 10000 characters") }
    if ($entry.features.Count -gt 20) { $problems.Add("$code has more than 20 features") }
    foreach ($f in $entry.features) { if ($f.Length -gt 200) { $problems.Add("$code feature over 200 characters: $f") } }
    if ($entry.terms.Count -gt 7) { $problems.Add("$code has more than 7 search terms") }
    $words = 0
    foreach ($t in $entry.terms) {
        if ($t.Length -gt 30) { $problems.Add("$code search term over 30 characters: $t") }
        $words += ($t -split '\s+' | Where-Object { $_ }).Count
    }
    if ($words -gt 21) { $problems.Add("$code search terms have $words words (limit 21)") }
    foreach ($key in $v.Keys) {
        if ($v[$key] -match '\{(T|AddFiles|AddFolder|Merge|Donate|Opt1|Opt2)\}') { $problems.Add("$code $key still has a placeholder") }
    }

    $values[$column] = $v
}

if ($problems.Count) {
    $problems | ForEach-Object { Write-Warning $_ }
    throw "Listing not written: $($problems.Count) problem(s)."
}

$rows = Import-Csv -Path $Template -Encoding UTF8
foreach ($row in $rows) {
    if (-not $row.Field) { continue }
    foreach ($column in $columns.Keys) {
        $v = $values[$column]
        if ($v.ContainsKey($row.Field)) { $row.$column = $v[$row.Field] }
    }
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$csvPath = Join-Path $OutDir 'listingData.csv'
$rows | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

Write-Output "listing -> $csvPath ($($columns.Count) languages)"
