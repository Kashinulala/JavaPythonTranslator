public class SimpleJavaExample {

    public static void main(String[] args) {
        int x = 10;
        int y = 5;
        double result = 0;
        boolean condition = true;
        String message = "Hello from Java!";

        if ((x < y) || condition) {
            result = x + y;
            System.out.println(message);
            System.out.println("The sum is: " + result);
        } else {
            result = x - y;
            System.out.println("The difference is: " + result);
        }

        for (int i = 0; i < 3; i++) {
            System.out.println("Loop iteration: " + i);
        }
    }
}