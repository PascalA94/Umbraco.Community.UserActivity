Automate `umbracoAudit.eventDetails` shapes from S3 (docs/spikes.md). Synthetic ids only.
- manual-run: `initiatedBy` = `user:<human key>` (seen on 17.5.1 and 18.5.1).
- scheduled: `scheduled:<id>`, not observed in S3; treated as automation.
- replay: `replay:<run id>`; the id is a run, never a user.
- unknown-shape: valid JSON without `initiatedBy`.
- too-deep: nesting over 16, skipped.
