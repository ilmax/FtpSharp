#!/usr/bin/env python3
import sys
import ftplib
import io
import os
import random
import string

port = int(sys.argv[1]) if len(sys.argv) > 1 else 2121
host = '127.0.0.1'

# Helper
def rand_bytes(n):
    return os.urandom(n)

def main():
    ftp = ftplib.FTP()
    ftp.connect(host, port, timeout=5)
    ftp.login('anonymous', '')

    # Passive mode RETR/STOR
    ftp.mkd('py')
    ftp.cwd('py')
    data = rand_bytes(4096)
    ftp.storbinary('STOR a.bin', io.BytesIO(data))
    out = io.BytesIO()
    ftp.retrbinary('RETR a.bin', out.write)
    assert out.getvalue() == data, 'Passive RETR mismatch'

    # Active mode: force active by disabling passive
    ftp.set_pasv(False)
    data2 = rand_bytes(2048)
    ftp.storbinary('STOR b.bin', io.BytesIO(data2))
    out2 = io.BytesIO()
    ftp.retrbinary('RETR b.bin', out2.write)
    assert out2.getvalue() == data2, 'Active RETR mismatch'

    # REST resume via retrbinary with rest argument
    big = rand_bytes(8192)
    ftp.storbinary('STOR c.bin', io.BytesIO(big))
    out3 = io.BytesIO()
    ftp.retrbinary('RETR c.bin', out3.write, rest=4096)
    assert out3.getvalue() == big[4096:], 'REST offset mismatch'

    # Clean up
    ftp.delete('a.bin')
    ftp.delete('b.bin')
    ftp.delete('c.bin')
    ftp.cwd('..')
    ftp.rmd('py')
    ftp.quit()

if __name__ == '__main__':
    main()
