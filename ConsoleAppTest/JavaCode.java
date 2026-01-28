public class SimpleJavaExample {

    public static void main(String[] args) {
        int x = 10;
        int y = 5;
        double result = 0;
        boolean condition = true;
        String message = "Hello from Java!";
        System.out.println("The sum is: " + x);
        

        if (x < y) {
            result = x + y;
            System.out.println(message);
            System.out.println("The sum is: " + result);
        } else {
            
            result = x - y;
            System.out.println("The difference is: " + result);
        }

        int x = 10;
        boolean condition = true;
        switch (x) {
            case condition:
                System.out.println(result + 1);
                break;
            case "":
                System.out.println(result + 3);
                break;
            default:
                break;
        }

        for (int i = 0; i < 3; i++) {
            if(i == 3){
                continue;
            }
            System.out.println("Loop iteration: " + i);
        }



        while (condition) {
            System.out.println("Loop iteration: " + i);
        }
    }
}