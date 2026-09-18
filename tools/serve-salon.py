#!/usr/bin/env python3
"""Serve the verified local WebGL builds with the headers required by Unity."""
import argparse
from pathlib import Path
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer

class Handler(SimpleHTTPRequestHandler):
    def end_headers(self):
        path=self.path.split('?',1)[0]
        if path.endswith('.br'):
            self.send_header('Content-Encoding','br')
            self.send_header('Content-Type','application/wasm' if path.endswith('.wasm.br') else 'application/javascript' if path.endswith('.js.br') else 'application/octet-stream')
        elif path.endswith('.gz'):
            self.send_header('Content-Encoding','gzip')
        self.send_header('Cross-Origin-Opener-Policy','same-origin')
        self.send_header('Cross-Origin-Embedder-Policy','require-corp')
        super().end_headers()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--port',type=int,default=8910)
    parser.add_argument('--bind',default='127.0.0.1',help='Use 0.0.0.0 for phone testing on the same local network')
    args=parser.parse_args()
    directory=Path(__file__).resolve().parents[1]/'unity-hair-salon/Builds'
    if not (directory/'WebGLDemo/index.html').is_file():raise SystemExit('Build WebGLDemo first.')
    server=ThreadingHTTPServer((args.bind,args.port),partial(Handler,directory=str(directory)))
    print(f'Demo: http://127.0.0.1:{server.server_port}/WebGLDemo/',flush=True)
    print(f'Asset Lab: http://127.0.0.1:{server.server_port}/WebGLAssetLab/',flush=True)
    try:server.serve_forever()
    except KeyboardInterrupt:pass
    finally:server.server_close()
if __name__=='__main__':main()
