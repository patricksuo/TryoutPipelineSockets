package main

import(
	"net"
	"io"
	"fmt"
	"flag"
	"encoding/binary"
)

var (
	g_port    int
)

func init() {
	flag.IntVar(&g_port, "p", 10008, "server port")
	flag.Parse()
}

func handleConnection(conn net.Conn) {
	defer conn.Close()
	readBuffer  := make([]byte, 128)
	writeBuffer  := make([]byte, 128)
	conn.(*net.TCPConn).SetNoDelay(true)
	var err error
	for {
		_, err  = io.ReadFull(conn, readBuffer[:4])
		if err != nil {
			return;
		}

		size := int(binary.LittleEndian.Uint32(readBuffer[:4]))
		if (len(readBuffer) < 4 + size) {
			readBuffer = make([]byte, 4 + size)
		}
		_, err  = io.ReadFull(conn, readBuffer[4:4+size])
		if err != nil {
			return;
		}

		if (len(writeBuffer) < 4 + size) {
			writeBuffer = make([]byte, 4 + size)
		}

		copy(writeBuffer[:4+size], readBuffer[:4+size])

		_, err = conn.Write(writeBuffer[:4+size])
		if err != nil {
			return
		}

	}
}

func main(){
	ln, err := net.Listen("tcp", fmt.Sprintf(":%d", g_port))
	if err != nil {
		panic(err)
	}
	for {
		conn, err := ln.Accept()
		if err != nil {
			panic(err)
		}
		go handleConnection(conn)
	}
}
