"""Loopback HTTP bridge for the official Unity stdio MCP server.

Configure ESP_MCP_AUTH_FILE, ESP_UNITY_PROJECT, and ESP_UNITY_CLI.
The private auth file contains clients with SHA-256 token digests and expiry.
Never publish the bridge without testing authentication through the tunnel.
"""
import hashlib
import hmac
import json
import os
from pathlib import Path
import time

from fastmcp.client.transports import StdioTransport
from fastmcp import FastMCP
from fastmcp.server.auth import AccessToken, TokenVerifier


class TeamTokenVerifier(TokenVerifier):
    async def verify_token(self, token: str) -> AccessToken | None:
        # Read on each request so revocation does not require a restart.
        try:
            clients = json.loads(Path(os.environ["ESP_MCP_AUTH_FILE"]).read_text())["clients"]
            digest = hashlib.sha256(token.encode()).hexdigest()
            for client in clients:
                if (client["expires_at"] > time.time()
                        and hmac.compare_digest(digest, client["sha256"])):
                    return AccessToken(
                        token=token, client_id=client["id"], subject=client["id"],
                        scopes=["editor"], expires_at=client["expires_at"],
                    )
        except (OSError, ValueError, KeyError, TypeError):
            pass  # Missing or malformed credentials fail closed.
        return None


if __name__ == "__main__":
    project = str(Path(os.environ["ESP_UNITY_PROJECT"]).resolve(strict=True))
    server = FastMCP.as_proxy(
        StdioTransport(os.environ["ESP_UNITY_CLI"], ["mcp", "--project-path", project]),
        name="ESP Unity", auth=TeamTokenVerifier(required_scopes=["editor"]),
    )
    # Session isolation does not isolate scene edits. Use one active editor owner.
    server.run(transport="http", host="127.0.0.1", port=8765)
