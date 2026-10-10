#!/usr/bin/env node
/**
 * OpenSSH ProxyCommand for ssh.railway.com.
 *
 * Windows OpenSSH often sends the client identification string before reading
 * the server banner. Railway's SSH endpoint then stalls ("banner exchange"
 * timeout). This proxy reads the server banner first, gives it to ssh, then
 * pipes bytes both ways.
 *
 * Usage (OpenSSH):
 *   ProxyCommand=node scripts/railway-ssh-proxy.mjs %h %p
 */
import net from "node:net";
import process from "node:process";

const host = process.argv[2] || "ssh.railway.com";
const port = Number(process.argv[3] || 22);

if (!Number.isFinite(port) || port <= 0) {
  console.error(`Invalid port: ${process.argv[3]}`);
  process.exit(2);
}

const sock = net.connect({ host, port, noDelay: true });
const stdinChunks = [];
let stdinEnded = false;
let ready = false;
let incoming = Buffer.alloc(0);

function fail(err) {
  const msg = err && err.message ? err.message : String(err);
  process.stderr.write(`railway-ssh-proxy: ${msg}\n`);
  process.exit(1);
}

sock.once("error", fail);
process.stdin.on("error", () => {});
process.stdout.on("error", () => {});

process.stdin.on("data", (chunk) => {
  if (!ready) {
    stdinChunks.push(chunk);
    return;
  }
  sock.write(chunk);
});

process.stdin.on("end", () => {
  if (!ready) {
    stdinEnded = true;
    return;
  }
  sock.end();
});

sock.on("data", (chunk) => {
  if (!ready) {
    incoming = Buffer.concat([incoming, chunk]);
    const nl = incoming.indexOf(0x0a);
    if (nl === -1) {
      return;
    }

    const banner = incoming.subarray(0, nl + 1);
    const rest = incoming.subarray(nl + 1);
    process.stdout.write(banner);
    ready = true;

    for (const buffered of stdinChunks) {
      sock.write(buffered);
    }
    stdinChunks.length = 0;
    if (stdinEnded) {
      sock.end();
    }

    if (rest.length) {
      process.stdout.write(rest);
    }
    return;
  }

  process.stdout.write(chunk);
});

sock.on("close", () => {
  process.exit(0);
});

sock.setTimeout(20000, () => {
  fail(new Error(`timed out connecting/reading banner from ${host}:${port}`));
});

sock.once("connect", () => {
  sock.setTimeout(0);
});

process.stdin.resume();
