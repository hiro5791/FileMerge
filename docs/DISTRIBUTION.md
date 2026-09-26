# Distribution and listings

Where TekuTeku File Merge is published or has been submitted, and what is still open.
Last updated: 2026-09-26.

## Published

| Where | URL | Notes |
| --- | --- | --- |
| GitHub Releases | https://github.com/hiro5791/FileMerge/releases/latest | 1.0.1 is the latest. Keep the 1.0.0 release: Softpedia links to its files. |
| Microsoft Store | https://apps.microsoft.com/detail/9MSSHDP4MJZK | Published 2026-09-25. Listings in 21 languages. See [STORE.md](STORE.md). |
| SourceForge | https://sourceforge.net/projects/tekuteku-merge/ | Imports every GitHub release automatically (GitHub Integration webhook). Logo not uploaded yet. |
| GitHub profile | https://github.com/hiro5791 | Profile README (repo `hiro5791/hiro5791`) lists the app; FileMerge is pinned. |
| tekuteku.online | https://tekuteku.online/2026/09/19/%e8%87%aa%e4%bd%9c%e3%82%a2%e3%83%97%e3%83%aa%e3%81%a8%e3%83%84%e3%83%bc%e3%83%ab/ (ja), https://tekuteku.online/en/2026/09/20/my-apps-and-tools/ (en) | App added to both "my apps" pages. |
| winget (msstore source) | `winget install 9MSSHDP4MJZK` | Automatic, because of the Store listing. |

## Submitted, waiting

| Where | Submitted | How | What happens next |
| --- | --- | --- | --- |
| 窓の杜 | 2026-09-25 | E-mail to mado-no-mori-info@impress.co.jp (1.0.0) | No reply unless they write an article or ask to include it in the library. |
| MajorGeeks | 2026-09-25 | E-mail to mgnews@majorgeeks.com (1.0.0) | The thread shows 2 messages: check for a reply. |
| Softpedia | 2026-09-26 | Regular submission form (download links point to the 1.0.0 files) | Up to 30 days; not guaranteed. Search Softpedia for "TekuTeku". |
| Portable Freeware Collection | 2026-09-26 | Forum topic in "Portable Freeware Submission" (user Hiroyura), 1.0.1 | First post needs moderator approval (usually < 24 h). Answer questions in the thread. Adding a database entry is possible 24 h after sign-up with one approved post. |
| AlternativeTo | 2026-09-26 | https://alternativeto.net/software/tekuteku-file-merge/ (user hiroyura), 8 alternatives linked | Private until reviewed; the free queue can take months. $5 priority review is optional (My submissions). Do not share the link yet. |
| Uptodown | 2026-09-26 | Developers Console (organization Hiroyura), app ID 1000876127, 1.0.1 installer from the GitHub release URL, English description, 4 screenshots | Status "Pending revision": editorial review. |
| FileHorse | 2026-09-26 | Contact form https://www.filehorse.com/contact/ with the 10 items from filehorse.com/submit (1.0.1 installer and portable links) | Tested by their team and VirusTotal; no stated timeline. |
| GitHub Sponsors | 2026-09-24 | Profile submitted for review | "A few days". Once approved: add it to the profile README and check the repo's Sponsor button. |

## Not done yet

| Where | Status |
| --- | --- |
| Vector | Author registration sent; waiting for the author number (PA…) by e-mail, then register the software (listing text drafted in the conversation). |
| FOSShub | Contact-form text drafted; send if not already sent. |
| winget community repo (`winget-pkgs`) | Not started. Would allow `winget install hiro5791.FileMerge` for the GitHub installer. |
| Neowin / gHacks | Not started (news tips). |
| Ninite | Not possible: Ninite adds only popular apps on its own. |

## Rules learned along the way

- Read the service's official documentation before guessing at a failure.
- Portable Freeware Collection cares about "stealth": the portable zips ship `portable.txt`, so an unzipped copy writes nothing outside its folder (verified for 1.0.1).
- Do not delete release tags: a draft release and its tag belong together.
