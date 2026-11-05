# Distribution and listings

Where TekuTeku File Merge is published or has been submitted, and what is still open.
Last updated: 2026-09-26.

## Published

| Where | URL | Notes |
| --- | --- | --- |
| GitHub Releases | https://github.com/hiro5791/FileMerge/releases/latest | 1.0.2 is the latest (adds readmes and the license to the portable zips). Keep the 1.0.0 release: Softpedia links to its files. |
| Microsoft Store | https://apps.microsoft.com/detail/9MSSHDP4MJZK | Published 2026-09-25. Listings in 21 languages. See [STORE.md](STORE.md). |
| SourceForge | https://sourceforge.net/projects/tekuteku-merge/ | Imports every GitHub release automatically (GitHub Integration webhook). Logo not uploaded yet. |
| GitHub profile | https://github.com/hiro5791 | Profile README (repo `hiro5791/hiro5791`) lists the app; FileMerge is pinned. |
| tekuteku.online | https://tekuteku.online/2026/09/19/%e8%87%aa%e4%bd%9c%e3%82%a2%e3%83%97%e3%83%aa%e3%81%a8%e3%83%84%e3%83%bc%e3%83%ab/ (ja), https://tekuteku.online/en/2026/09/20/my-apps-and-tools/ (en) | App added to both "my apps" pages. |
| AlternativeTo | https://alternativeto.net/software/tekuteku-file-merge/ | Approved 2026-09-26. 8 alternatives linked (GSplit, Splitcat, ...). Anyone can suggest edits; they are reviewed. |
| GitHub Sponsors | https://github.com/sponsors/hiro5791 | Approved 2026-09-27. Tiers: monthly $1, $5; one-time $3, $10 (no rewards). Listed in the repo Sponsor button (FUNDING.yml), both READMEs and the profile README. |
| winget (msstore source) | `winget install 9MSSHDP4MJZK` | Automatic, because of the Store listing. |

## Submitted, waiting

| Where | Submitted | How | What happens next |
| --- | --- | --- | --- |
| 窓の杜 | 2026-09-25 | E-mail to mado-no-mori-info@impress.co.jp (1.0.0) | No reply unless they write an article or ask to include it in the library. |
| MajorGeeks | 2026-09-25 | E-mail to mgnews@majorgeeks.com (1.0.0) | The thread shows 2 messages: check for a reply. |
| Softpedia | 2026-09-26 | Regular submission form (download links point to the 1.0.0 files) | Up to 30 days; not guaranteed. Search Softpedia for "TekuTeku". |
| Portable Freeware Collection | 2026-09-26 | Forum topic in "Portable Freeware Submission" (user Hiroyura), 1.0.1 | First post needs moderator approval (usually < 24 h). Answer questions in the thread. Adding a database entry is possible 24 h after sign-up with one approved post. |
| Uptodown | 2026-09-26 | Developers Console (organization Hiroyura), app ID 1000876127, 1.0.1 installer from the GitHub release URL, English description, 4 screenshots | Status "Pending revision": editorial review. |
| FileHorse | 2026-09-26 | Contact form https://www.filehorse.com/contact/ with the 10 items from filehorse.com/submit (1.0.1 installer and portable links) | Tested by their team and VirusTotal; no stated timeline. |
| Vector | 2026-11-05 | Author PA051551, software page se_no=529760, file PS792218 = FileMerge-1.0.2-win-x64.zip (56,180,385 bytes, MD5 18030acf28e09d704751a05b545207a5) | "Publication requested". Vector refused 1.0.1 (PS791116) on 2026-10-29 because the zip had no user document; 1.0.2 adds readme_ja.txt, readme.txt and LICENSE.txt. PS791116 and the mistaken 1.0.0 upload PS791114 are stopped by Vector. |
| winget (winget-pkgs) | 2026-09-26 | PR https://github.com/microsoft/winget-pkgs/pull/441636 from the fork hiro5791/winget-pkgs, branch hiro5791.FileMerge-1.0.1; manifest in packaging/winget | Automated validation, then moderator review. Sign the CLA when the bot asks. Answer Needs-Author-Feedback within 10 days. Then `winget install hiro5791.FileMerge`. |

## Not done yet

| Where | Status |
| --- | --- |
| FOSShub | Contact-form text drafted; send if not already sent. |

## Rules learned along the way

- Read the service's official documentation before guessing at a failure.
- Portable Freeware Collection cares about "stealth": the portable zips ship `portable.txt`, so an unzipped copy writes nothing outside its folder (verified for 1.0.1).
- Do not delete release tags: a draft release and its tag belong together.
- Download sites such as Vector refuse an archive without a user document: every portable zip ships readme.txt, readme_ja.txt and LICENSE.txt (from 1.0.2).
- When choosing a file to upload, check the full file name in the file dialog: the page shortens it to "FileMerge…win-x64.zip".
