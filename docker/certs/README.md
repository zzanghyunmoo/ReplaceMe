# Extra local CA certificates

Place local development CA certificates (`*.crt`) in this directory when Docker
builds need to trust an internal HTTPS proxy for NuGet or npm access.

Do not commit private/internal certificate files unless the team has explicitly
approved that distribution. This repository ignores `docker/certs/*.crt` by
default, while the Dockerfiles copy the directory at build time and run
`update-ca-certificates` when one or more `.crt` files are present.
